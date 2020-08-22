using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using static Winux.Winux.ServicesAPI;

namespace Winux
{
    public static class Winux
    {
        private static Action _start;
        private static Action _stop;
        static Action<string>  _onMessage;
        public static int Run(Action onStart, Action onStop, Action<string> onMessage)
        {
            _onMessage = onMessage;
            _start = onStart;
            _stop = onStop;
            if (IamNTService)
                return RunNT();
            else return RunConsole();
        }

        public static int RunConsole()
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += OnAssemblyLoadContextUnloading;
            System.AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                _onMessage("CurrentDomain.ProcessExit");
                __stop();
            };
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _onMessage("CancelKeyPress");
                __stop();
            };
            if (IamWindows)
                SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            _start();
            return 0;
        }

        private volatile static bool ___stop;
        private static void __stop()
        {
            if (___stop)
                return;
            ___stop = true;
            _stop();
        }

        private static void OnAssemblyLoadContextUnloading(AssemblyLoadContext sender)
        {
            _onMessage("OnAssemblyLoadContextUnloading");
            __stop();
        }


        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            _onMessage("ConsoleCtrlCheck:" + ctrlType.ToString());
            __stop();
            return true;
        }

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }


        private static string Name
        {
            get
            {
                return Assembly.GetEntryAssembly().GetName().Name;
            }
        }

        public static int RunNT()
        {
            ServicesAPI.SERVICE_TABLE_ENTRY entry = new ServicesAPI.SERVICE_TABLE_ENTRY();
            entry.lpServiceName = Name;
            entry.lpServiceProc = new ServicesAPI.ServiceMainProc(baseServiceMain);
            ServicesAPI.SERVICE_TABLE_ENTRY[] table = new ServicesAPI.SERVICE_TABLE_ENTRY[] 
            { entry,
                new SERVICE_TABLE_ENTRY() { lpServiceName = null, lpServiceProc = null }
            };
            var x = ServicesAPI.StartServiceCtrlDispatcher(table);
            var y = Marshal.GetLastWin32Error();
            if (x == 0)
            {
                switch (y)
                {
                    case ServicesAPI.ERROR_INVALID_DATA:
                        throw new ApplicationException(
                            "The specified dispatch table contains entries that are not in the proper format.");
                    case ServicesAPI.ERROR_SERVICE_ALREADY_RUNNING:
                        throw new ApplicationException("A service is already running.");
                    case ServicesAPI.ERROR_FAILED_SERVICE_CONTROLLER_CONNECT:
                        //_start();
                        break;
                    default:
                        throw new ApplicationException(
                            "An unknown error occurred while starting up the service(s).");
                }
            }
            return 0;
        }

        private static void baseServiceMain(int argc, string[] argv)
        {
            _servStatusHandle = ServicesAPI.RegisterServiceCtrlHandlerEx(Name, _servCtrlHandlerProc, IntPtr.Zero);
            if (_servStatusHandle == IntPtr.Zero) return;
             _servStatus.dwServiceType = ServicesAPI.ServiceType.SERVICE_WIN32_OWN_PROCESS | ServicesAPI.ServiceType.SERVICE_WIN32_SHARE_PROCESS;

            _servStatus.dwCurrentState = ServicesAPI.ServiceCurrentStateType.SERVICE_START_PENDING;
            _servStatus.dwControlsAccepted = ServicesAPI.ControlsAccepted.SERVICE_ACCEPT_STOP;
             _servStatus.dwWin32ExitCode = 0;
             _servStatus.dwServiceSpecificExitCode = 0;
             _servStatus.dwCheckPoint = 0;
             _servStatus.dwWaitHint = 0;
             _servStatusHandle = ServicesAPI.RegisterServiceCtrlHandlerEx( Name,  _servCtrlHandlerProc, IntPtr.Zero);
            if (_servStatusHandle == IntPtr.Zero) return;
             _servStatus.dwCurrentState = ServicesAPI.ServiceCurrentStateType.SERVICE_RUNNING;
             _servStatus.dwCheckPoint = 0;
             _servStatus.dwWaitHint = 0;
            if (ServicesAPI.SetServiceStatus( _servStatusHandle, ref  _servStatus) == 0)
            {
                var errorid = Marshal.GetLastWin32Error();
                throw new ApplicationException("\""  + "\" threw an error. Error Number: " + errorid);
            }
            _start();

        }

        private static IntPtr _servStatusHandle;
        private static ServicesAPI.SERVICE_STATUS _servStatus;

        private static ServiceCtrlHandlerProcEx _servCtrlHandlerProc = new ServicesAPI.ServiceCtrlHandlerProcEx(baseServiceControlHandler);

        private static int baseServiceControlHandler(ServicesAPI.ServiceControlType Opcode, int eventType, IntPtr eventData, IntPtr context)
        {
            switch (Opcode)
            {
                case ServicesAPI.ServiceControlType.SERVICE_CONTROL_STOP:
                    _servStatus.dwWin32ExitCode = 0;
                    _servStatus.dwCurrentState = ServicesAPI.ServiceCurrentStateType.SERVICE_STOPPED;
                    _servStatus.dwCheckPoint = 0;
                    _servStatus.dwWaitHint = 0;
                    ServicesAPI.SetServiceStatus(_servStatusHandle, ref _servStatus);
                    _stop();
                    break;
                default:
                    break;
            }
            return 0; //NO_ERROR
        }


        #region ServiceApi
        ///<summary>A collection of Win32 API functions and structs for use with Win32 services.</summary>
        public static class ServicesAPI
        {

            /// <summary>
            /// An application-defined callback function used with the RegisterServiceCtrlHandlerEx function. A service program can use it as the control handler function of a particular service.
            /// </summary>
            /// <param name="dwControl">The control code.</param>
            /// <param name="dwEventType">The type of event that has occurred</param>
            /// <param name="lpEventData"></param>
            /// <param name="lpContext"></param>
            /// <returns></returns>
            public delegate int ServiceCtrlHandlerProcEx(ServiceControlType dwControl, int dwEventType, IntPtr lpEventData, IntPtr lpContext);


            /// <summary>
            /// Registers a function to handle extended service control requests.
            /// </summary>
            /// <param name="lpServiceName">The name of the service run by the calling thread. This is the service name that the service control program specified in the CreateService function when creating the service.</param>
            /// <param name="lpHandlerProc">A pointer to the handler function to be registered. For more information, see HandlerEx.</param>
            /// <param name="lpContext">Any user-defined data. This parameter, which is passed to the handler function, can help identify the service when multiple services share a process.</param>
            /// <returns>
            ///   If the function succeeds, the return value is a service status handle.If the function fails, the return value is zero. To get extended error information, call GetLastError.
            /// <para>ERROR_NOT_ENOUGH_MEMORY Not enough memory is available to convert an ANSI string parameter to Unicode. This error does not occur for Unicode string parameters.</para>  
            /// <para>ERROR_SERVICE_NOT_IN_EXE The service entry was specified incorrectly when the process called the StartServiceCtrlDispatcher function.</para>
            /// </returns>
            [DllImport("advapi32.dll", EntryPoint = "RegisterServiceCtrlHandlerEx", SetLastError = true)]
            internal static extern IntPtr RegisterServiceCtrlHandlerEx(string lpServiceName, ServiceCtrlHandlerProcEx lpHandlerProc, IntPtr lpContext);

            /// <summary>
            /// Updates the service control manager's status information for the calling service.
            /// </summary>
            /// <param name="hServiceStatus">A handle to the status information structure for the current service. This handle is returned by the RegisterServiceCtrlHandlerEx function.</param>
            /// <param name="lpServiceStatus">A pointer to the SERVICE_STATUS structure the contains the latest status information for the calling service.</param>
            /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call GetLastError.</returns>
            [DllImport("advapi32.dll", SetLastError = true)]
            internal static extern int SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

            /// <summary>
            /// Connects the main thread of a service process to the service control manager, which causes the thread to be the service control dispatcher thread for the calling process.
            /// </summary>
            /// <param name="lpServiceStartTable">A pointer to an array of SERVICE_TABLE_ENTRY structures containing one entry for each service that can execute in the calling process. The members of the last entry in the table must have NULL values to designate the end of the table.</param>
            /// <returns>If the function succeeds, the return value is nonzero.</returns>
            [DllImport("advapi32.dll", EntryPoint = "StartServiceCtrlDispatcher", SetLastError = true)]
            internal static extern int StartServiceCtrlDispatcher(SERVICE_TABLE_ENTRY[] lpServiceStartTable);





            public const int ERROR_FAILED_SERVICE_CONTROLLER_CONNECT = 1063;
            public const int ERROR_INVALID_DATA = 13;
            public const int ERROR_SERVICE_ALREADY_RUNNING = 1057;


            public const int SERVICE_NO_CHANGE = 0xFFFF;


            [Flags]
            public enum ServiceType : int
            {
                /// <summary>Driver service.</summary>
                SERVICE_KERNEL_DRIVER = 0x1,
                /// <summary>File system driver service.</summary>
                SERVICE_FILE_SYSTEM_DRIVER = 0x2,
                /// <summary>Service that runs in its own process.</summary>
                SERVICE_WIN32_OWN_PROCESS = 0x10,
                /// <summary>Service that shares a process with one or more other services. </summary>
                SERVICE_WIN32_SHARE_PROCESS = 0x20,
                /// <summary>The service can interact with the desktop.</summary>
                SERVICE_INTERACTIVE_PROCESS = 0x100,
                SERVICETYPE_NO_CHANGE = SERVICE_NO_CHANGE
            }



            /// <summary>
            /// Notifies a service of device events. (The service must have registered to receive these notifications using the RegisterDeviceNotification function.)
            /// </summary>
            public enum SERVICE_CONTROL_DEVICEEVENT_Control : int
            {
                /// <summary>system detected a new device</summary>
                DBT_DEVICEARRIVAL = 0x8000,
                /// <summary>wants to remove, may fail</summary>
                DBT_DEVICEQUERYREMOVE = 0x8001,
                /// <summary>removal aborted</summary>
                DBT_DEVICEQUERYREMOVEFAILED = 0x8002,
                /// <summary>about to remove, still avail.</summary>
                DBT_DEVICEREMOVEPENDING = 0x8003,
                /// <summary>device is gone</summary>
                DBT_DEVICEREMOVECOMPLETE = 0x8004,
                /// <summary>type specific event</summary>
                DBT_DEVICETYPESPECIFIC = 0x8005,
                /// <summary>user-defined event</summary>
                DBT_CUSTOMEVENT = 0x8006,
            }

            /// <summary>
            /// Notifies a service that the computer's hardware profile has changed.
            /// </summary>
            public enum SERVICE_CONTROL_HARDWAREPROFILECHANGE_Control : int
            {
                /// <summary>sent when a config has changed</summary>
                DBT_CONFIGCHANGED = 0x0018,
                /// <summary>sent to ask if a config change is allowed</summary>
                DBT_QUERYCHANGECONFIG = 0x0017,
                /// <summary>someone cancelled the config change</summary>
                DBT_CONFIGCHANGECANCELED = 0x0019
            }

            /// <summary>
            /// The power-management event.
            /// </summary>
            public enum SERVICE_CONTROL_POWEREVENT_Control : int
            {
                /// <summary>
                /// Requested to suspend the computer. 
                /// </summary>
                PBT_APMQUERYSUSPEND = 0x0000,
                /// <summary>
                /// Requested to suspend the computer
                /// </summary>
                PBT_APMQUERYSTANDBY = 0x0001,
                /// <summary>
                /// permission to suspend the computer was denied.
                /// </summary>
                PBT_APMQUERYSUSPENDFAILED = 0x0002,
                /// <summary>
                /// permission to standby the computer was denied
                /// </summary>
                PBT_APMQUERYSTANDBYFAILED = 0x0003,
                /// <summary>
                /// the computer is about to enter a suspended state
                /// <para>An application should process this event by completing all tasks necessary to save data. This event may also be broadcast, without a prior PBT_APMQUERYSUSPEND event, if an application or device driver uses the SetSystemPowerState function to force suspension.</para>
                /// <para>The system allows approximately two seconds for an application to handle this notification. If an application is still performing operations after its time allotment has expired, the system may interrupt the application.</para>
                /// <para>Windows Server 2003 and Windows XP:  Applications are allowed up to 20 seconds to respond to the PBT_APMSUSPEND event.</para>
                /// </summary>
                PBT_APMSUSPEND = 0x0004,
                /// <summary>
                /// the computer is about to enter the standby state
                /// </summary>
                PBT_APMSTANDBY = 0x0005,
                /// <summary>
                /// Notifies applications that the system has resumed operation. This event can indicate that some or all applications did not receive a PBT_APMSUSPEND event. For example, this event can be broadcast after a critical suspension caused by a failing battery.
                /// </summary>
                PBT_APMRESUMECRITICAL = 0x0006,
                /// <summary>
                /// Operation is resuming from a low-power state. This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.
                /// <para>An application can receive this event only if it received the PBT_APMSUSPEND event before the computer was suspended. Otherwise, the application will receive a PBT_APMRESUMECRITICAL event.</para>
                /// <para>If the system wakes due to user activity (such as pressing the power button) or if the system detects user interaction at the physical console (such as mouse or keyboard input) after waking unattended, the system first broadcasts the PBT_APMRESUMEAUTOMATIC event, then it broadcasts the PBT_APMRESUMESUSPEND event. In addition, the system turns on the display. Your application should reopen files that it closed when the system entered sleep and prepare for user input.</para>
                /// <para>If the system wakes due to an external wake signal (remote wake), the system broadcasts only the PBT_APMRESUMEAUTOMATIC event. The PBT_APMRESUMESUSPEND event is not sent.</para>
                /// <para>Windows Server 2003 and Windows XP:  If an application called SetSystemPowerState with fForce set to TRUE or the system performed a critical suspend, the system will broadcast a PBT_APMRESUMECRITICAL event after waking.</para>
                /// </summary>
                PBT_APMRESUMESUSPEND = 0x0007,
                /// <summary>
                /// Operation is resuming from a standby
                /// </summary>
                PBT_APMRESUMESTANDBY = 0x0008,
                /// <summary>
                /// indicates that a suspend operation failed after the PBT_APMSUSPEND event was broadcast.
                /// </summary>
                PBTF_APMRESUMEFROMFAILURE = 0x00000001,
                /// <summary>
                /// Notifies applications that the battery power is low.
                /// </summary>
                PBT_APMBATTERYLOW = 0x0009,
                /// <summary>
                /// Power status has changed.
                /// <para>An application should process this event by calling the GetSystemPowerStatus function to retrieve the current power status of the computer. In particular, the application should check the ACLineStatus, BatteryFlag, BatteryLifeTime, and BatteryLifePercent members of the SYSTEM_POWER_STATUS structure for any changes. This event can occur when battery life drops to less than 5 minutes, or when the percentage of battery life drops below 10 percent, or if the battery life changes by 3 percent.</para>
                /// </summary>
                PBT_APMPOWERSTATUSCHANGE = 0x000A,
                /// <summary>
                /// Notifies applications that the APM BIOS has signaled an APM OEM event.
                /// </summary>
                PBT_APMOEMEVENT = 0x000B,
                /// <summary>
                /// Operation is resuming automatically from a low-power state. This message is sent every time the system resumes.
                /// <para>If the system detects any user activity after broadcasting PBT_APMRESUMEAUTOMATIC, it will broadcast a PBT_APMRESUMESUSPEND event to let applications know they can resume full interaction with the user.</para>
                /// </summary>
                PBT_APMRESUMEAUTOMATIC = 0x0012,
                /// <summary>
                /// A power setting change event has been received.
                /// </summary>
                PBT_POWERSETTINGCHANGE = 0x8013
            }

            public enum SERVICE_CONTROL_SESSIONCHANGE_Control : int
            {
                WTS_CONSOLE_CONNECT = 0x1,
                WTS_CONSOLE_DISCONNECT = 0x2,
                WTS_REMOTE_CONNECT = 0x3,
                WTS_REMOTE_DISCONNECT = 0x4,
                WTS_SESSION_LOGON = 0x5,
                WTS_SESSION_LOGOFF = 0x6,
                WTS_SESSION_LOCK = 0x7,
                WTS_SESSION_UNLOCK = 0x8,
                WTS_SESSION_REMOTE_CONTROL = 0x9,
                WTS_SESSION_CREATE = 0xa,
                WTS_SESSION_TERMINATE = 0xb
            }

            public enum ServiceControlType : int
            {
                /// <summary>
                /// Notifies a service that it should stop.
                /// <para>If a service accepts this control code, it must stop upon receipt and return NO_ERROR. After the SCM sends this control code, it will not send other control codes to the service.</para>
                /// <para>Windows XP:  If the service returns NO_ERROR and continues to run, it continues to receive control codes. This behavior changed starting with Windows Server 2003 and Windows XP with SP2.</para>
                /// </summary>
                SERVICE_CONTROL_STOP = 0x00000001,
                /// <summary>
                /// Notifies a service that it should pause.
                /// </summary>
                SERVICE_CONTROL_PAUSE = 0x00000002,
                /// <summary>Notifies a paused service that it should resume.</summary>
                SERVICE_CONTROL_CONTINUE = 0x00000003,
                /// <summary>Notifies a service to report its current status information to the service control manager.
                /// <para>The handler should simply return NO_ERROR; the SCM is aware of the current state of the service.</para>
                /// </summary>
                SERVICE_CONTROL_INTERROGATE = 0x00000004,
                /// <summary>
                /// Notifies a service that the system is shutting down so the service can perform cleanup tasks. Note that services that register for SERVICE_CONTROL_PRESHUTDOWN notifications cannot receive this notification because they have already stopped.
                /// <para>If a service accepts this control code, it must stop after it performs its cleanup tasks and return NO_ERROR. After the SCM sends this control code, it will not send other control codes to the service.</para>
                /// </summary>
                SERVICE_CONTROL_SHUTDOWN = 0x00000005,
                /// <summary>
                /// Notifies a service that service-specific startup parameters have changed. The service should reread its startup parameters.
                /// </summary>
                SERVICE_CONTROL_PARAMCHANGE = 0x00000006,
                /// <summary>
                /// Notifies a network service that there is a new component for binding. The service should bind to the new component.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                SERVICE_CONTROL_NETBINDADD = 0x00000007,
                /// <summary>
                /// Notifies a network service that a component for binding has been removed. The service should reread its binding information and unbind from the removed component.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                SERVICE_CONTROL_NETBINDREMOVE = 0x00000008,
                /// <summary>
                /// Notifies a network service that a disabled binding has been enabled. The service should reread its binding information and add the new binding.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                SERVICE_CONTROL_NETBINDENABLE = 0x00000009,
                /// <summary>
                /// Notifies a network service that one of its bindings has been disabled. The service should reread its binding information and remove the binding.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                SERVICE_CONTROL_NETBINDDISABLE = 0x0000000A,
                /// <summary>
                /// Notifies a service of device events. (The service must have registered to receive these notifications using the RegisterDeviceNotification function.) The dwEventType and lpEventData parameters contain additional information.
                /// </summary>
                SERVICE_CONTROL_DEVICEEVENT = 0x0000000B,
                /// <summary>
                /// Notifies a service that the computer's hardware profile has changed. The dwEventType parameter contains additional information.
                /// </summary>
                SERVICE_CONTROL_HARDWAREPROFILECHANGE = 0x0000000C,
                /// <summary>
                /// Notifies a service of system power events. The dwEventType parameter contains additional information. If dwEventType is PBT_POWERSETTINGCHANGE, the lpEventData parameter also contains additional information.
                /// </summary>
                SERVICE_CONTROL_POWEREVENT = 0x0000000D,
                /// <summary>
                /// Notifies a service of session change events. Note that a service will only be notified of a user logon if it is fully loaded before the logon attempt is made. The dwEventType and lpEventData parameters contain additional information.
                /// </summary>
                SERVICE_CONTROL_SESSIONCHANGE = 0x0000000E,
                /// <summary>
                /// Notifies a service that the system will be shutting down. Services that need additional time to perform cleanup tasks beyond the tight time restriction at system shutdown can use this notification. The service control manager sends this notification to applications that have registered for it before sending a SERVICE_CONTROL_SHUTDOWN notification to applications that have registered for that notification.
                /// <para>A service that handles this notification blocks system shutdown until the service stops or the preshutdown time-out interval specified through SERVICE_PRESHUTDOWN_INFO expires. Because this affects the user experience, services should use this feature only if it is absolutely necessary to avoid data loss or significant recovery time at the next system start.</para>
                /// <para>Windows Server 2003 and Windows XP:  This value is not supported.</para>
                /// </summary>
                SERVICE_CONTROL_PRESHUTDOWN = 0x0000000F,
                /// <summary>
                /// Notifies a service that the system time has changed. The lpEventData parameter contains additional information. The dwEventType parameter is not used.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                SERVICE_CONTROL_TIMECHANGE = 0x00000010,
                /// <summary>
                /// Notifies a service registered for a service trigger event that the event has occurred.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                SERVICE_CONTROL_TRIGGEREVENT = 0x00000020
            }

            public enum ServiceCurrentStateType : int
            {
                SERVICE_STOPPED = 0x00000001,
                SERVICE_START_PENDING = 0x00000002,
                SERVICE_STOP_PENDING = 0x00000003,
                SERVICE_RUNNING = 0x00000004,
                SERVICE_CONTINUE_PENDING = 0x00000005,
                SERVICE_PAUSE_PENDING = 0x00000006,
                SERVICE_PAUSED = 0x00000007
            }

            /// <summary>
            /// The control codes the service accepts and processes in its handler function (see Handler and HandlerEx). 
            /// A user interface process can control a service by specifying a control command in the ControlService or ControlServiceEx function. 
            /// By default, all services accept the SERVICE_CONTROL_INTERROGATE value. To accept the SERVICE_CONTROL_DEVICEEVENT value, the service 
            /// must register to receive device events by using the RegisterDeviceNotification function.
            /// </summary>
            [Flags]
            public enum ControlsAccepted : int
            {
                SERVICE_ACCEPT_ALL = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_PAUSE_CONTINUE | SERVICE_ACCEPT_SHUTDOWN | SERVICE_ACCEPT_PARAMCHANGE | SERVICE_ACCEPT_NETBINDCHANGE |
                                      SERVICE_ACCEPT_HARDWAREPROFILECHANGE | SERVICE_ACCEPT_POWEREVENT | SERVICE_ACCEPT_SESSIONCHANGE | SERVICE_ACCEPT_PRESHUTDOWN | SERVICE_ACCEPT_TIMECHANGE |
                                      SERVICE_ACCEPT_TRIGGEREVENT,
                /// <summary>
                /// The service can be stopped.
                /// </summary>
                SERVICE_ACCEPT_STOP = 0x00000001,
                /// <summary>
                /// The service can be paused and continued.
                /// </summary>
                SERVICE_ACCEPT_PAUSE_CONTINUE = 0x00000002,
                /// <summary>
                /// The service is notified when system shutdown occurs.
                /// </summary>
                SERVICE_ACCEPT_SHUTDOWN = 0x00000004,
                /// <summary>
                /// The service can reread its startup parameters without being stopped and restarted.
                /// </summary>
                SERVICE_ACCEPT_PARAMCHANGE = 0x00000008,
                /// <summary>
                /// The service is a network component that can accept changes in its binding without being stopped and restarted.
                /// </summary>
                SERVICE_ACCEPT_NETBINDCHANGE = 0x00000010,
                /// <summary>
                /// The service is notified when the computer's hardware profile has changed. This enables the system to send SERVICE_CONTROL_HARDWAREPROFILECHANGE notifications to the service.
                /// </summary>
                SERVICE_ACCEPT_HARDWAREPROFILECHANGE = 0x00000020,
                /// <summary>
                /// The service is notified when the computer's power status has changed. This enables the system to send SERVICE_CONTROL_POWEREVENT notifications to the service.
                /// </summary>
                SERVICE_ACCEPT_POWEREVENT = 0x00000040,
                /// <summary>
                /// The service is notified when the computer's session status has changed. This enables the system to send SERVICE_CONTROL_SESSIONCHANGE notifications to the service.
                /// </summary>
                SERVICE_ACCEPT_SESSIONCHANGE = 0x00000080,
                /// <summary>
                /// The service can perform preshutdown tasks.
                /// </summary>
                SERVICE_ACCEPT_PRESHUTDOWN = 0x00000100,
                /// <summary>
                /// The service is notified when the system time has changed. This enables the system to send SERVICE_CONTROL_TIMECHANGE notifications to the service.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                SERVICE_ACCEPT_TIMECHANGE = 0x00000200,
                /// <summary>
                /// The service is notified when an event for which the service has registered occurs. This enables the system to send SERVICE_CONTROL_TRIGGEREVENT notifications to the service.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                SERVICE_ACCEPT_TRIGGEREVENT = 0x00000400,
                /// <summary>
                /// The services is notified when the user initiates a reboot.
                /// <para>Windows Server 2008 R2, Windows 7, Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                SERVICE_ACCEPT_USERMODEREBOOT = 0x00000800
            }

            public enum NetBindControl
            {
                NETBINDADD,
                NETBINDDISABLE,
                NETBINDENABLE,
                NETBINDREMOVE
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct POWERBROADCAST_SETTING
            {
                public Guid PowerSetting;
                public Int32 DataLength;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct WTSSESSION_NOTIFICATION
            {
                public uint cbSize;
                public uint dwSessionId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SERVICE_TIMECHANGE_INFO
            {
                public long liNewTime;
                public long liOldTime;

                public DateTime liNewTimeToDateTime()
                {
                    return DateTime.FromFileTime(liNewTime);
                }

                public DateTime liOldTimeToDateTime()
                {
                    return DateTime.FromFileTime(liOldTime);
                }
            }



            [StructLayout(LayoutKind.Sequential)]
            public struct SERVICE_STATUS
            {
                public ServiceType dwServiceType;
                public ServiceCurrentStateType dwCurrentState;
                public ControlsAccepted dwControlsAccepted;
                public int dwWin32ExitCode;
                public int dwServiceSpecificExitCode;
                public int dwCheckPoint;
                public int dwWaitHint;
            }


            public delegate void ServiceMainProc(int argc, [MarshalAs(UnmanagedType.LPArray)]string[] argv);
            public struct SERVICE_TABLE_ENTRY
            {
                public string lpServiceName;
                [MarshalAs(UnmanagedType.FunctionPtr)]
                public ServiceMainProc lpServiceProc;

            }
        }
        #endregion

        #region WindowService
        private static bool? _iamWindowsAndIamService;

        public static bool IamNTService
        {
            get
            {
                if (_iamWindowsAndIamService == null)
                {
                    if (IamWindows)
                    {
                        var p = ParentProcessUtilities.GetParentProcess();
                        _iamWindowsAndIamService = (p != null && p.ProcessName == "services");
                    }
                    else
                    {
                        _iamWindowsAndIamService = false;
                    }
                }

                return (bool)_iamWindowsAndIamService;
            }
        }

        public static bool IamWinConsole
        {
            get
            {
                return IamWindows && !IamNTService;
            }
        }

        public static bool IamUnix
        {
            get
            {
                return !IamWindows;
            }
        }

        private static bool? _iamWindows;

        public static bool IamWindows
        {
            get
            {
                if (_iamWindows == null)
                    _iamWindows =
                        (Environment.OSVersion.Platform == PlatformID.Win32NT ||
                         Environment.OSVersion.Platform == PlatformID.Win32S ||
                         Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                         Environment.OSVersion.Platform == PlatformID.WinCE);
                return (bool)_iamWindows;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ParentProcessUtilities
        {
            internal IntPtr Reserved1;
            internal IntPtr PebBaseAddress;
            internal IntPtr Reserved2_0;
            internal IntPtr Reserved2_1;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;

            [DllImport("ntdll.dll")]
            private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
                ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);

            public static Process GetParentProcess()
            {
                return GetParentProcess(Process.GetCurrentProcess().Handle);
            }

            public static Process GetParentProcess(int id)
            {
                var process = Process.GetProcessById(id);
                return GetParentProcess(process.Handle);
            }

            private static Process GetParentProcess(IntPtr handle)
            {
                var pbi = new ParentProcessUtilities();
                var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
                if (status != 0)
                    throw new Win32Exception(status);
                try
                {
                    return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
                }
                catch (ArgumentException)
                {
                    // not found
                    return null;
                }
            }
        }
        #endregion
    }
}
