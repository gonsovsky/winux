using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using static Winux.Winux.ServicesApi;

namespace Winux
{
    public static class Winux
    {
        #region startUp
        private static Action<string[]> _start;

        private static Action _stop;

        private static Action<string>  _onMessage;

        private static string[] _args;

        private static volatile bool _stopRequested;

        public static int Run(string[] args, Action<string[]> onStart, Action onStop, Action<string> onMessage)
        {
            _onMessage = onMessage;
            _start = onStart;
            _stop = onStop;
            _args = args;
            if (IamNtService)
                return RunNt();
            else return RunConsole();
        }

        private static void __stop()
        {
            if (_stopRequested)
                return;
            _stopRequested = true;
            _stop();
        }
        #endregion

        #region NT
        private static string Name => Assembly.GetEntryAssembly()?.GetName().Name;

        public static int RunNt()
        {
            ServiceTableEntry entry = new ServiceTableEntry();
            entry.LpServiceName = Name;
            entry.LpServiceProc = BaseServiceMain;
            ServiceTableEntry[] table = new ServiceTableEntry[] 
            { entry,
                new ServiceTableEntry() { LpServiceName = null, LpServiceProc = null }
            };
            var x = StartServiceCtrlDispatcher(table);
            var y = Marshal.GetLastWin32Error();
            if (x == 0)
            {
                switch (y)
                {
                    case ErrorInvalidData:
                        throw new ApplicationException(
                            "The specified dispatch table contains entries that are not in the proper format.");
                    case ErrorServiceAlreadyRunning:
                        throw new ApplicationException("A service is already running.");
                    case ErrorFailedServiceControllerConnect:
                        //_start();
                        break;
                    default:
                        throw new ApplicationException(
                            "An unknown error occurred while starting up the service(s).");
                }
            }
            return 0;
        }

        private static void BaseServiceMain(int argc, string[] argv)
        {
            _servStatusHandle = RegisterServiceCtrlHandlerEx(Name, ServCtrlHandlerProc, IntPtr.Zero);
            if (_servStatusHandle == IntPtr.Zero) return;
             _servStatus.dwServiceType = ServiceType.ServiceWin32OwnProcess | ServiceType.ServiceWin32ShareProcess;

            _servStatus.dwCurrentState = ServiceCurrentStateType.ServiceStartPending;
            _servStatus.dwControlsAccepted = ControlsAccepted.ServiceAcceptStop;
             _servStatus.dwWin32ExitCode = 0;
             _servStatus.dwServiceSpecificExitCode = 0;
             _servStatus.dwCheckPoint = 0;
             _servStatus.dwWaitHint = 0;
             _servStatusHandle = RegisterServiceCtrlHandlerEx( Name,  ServCtrlHandlerProc, IntPtr.Zero);
            if (_servStatusHandle == IntPtr.Zero) return;
             _servStatus.dwCurrentState = ServiceCurrentStateType.ServiceRunning;
             _servStatus.dwCheckPoint = 0;
             _servStatus.dwWaitHint = 0;
            if (SetServiceStatus( _servStatusHandle, ref  _servStatus) == 0)
            {
                var errorid = Marshal.GetLastWin32Error();
                throw new ApplicationException("\""  + "\" threw an error. Error Number: " + errorid);
            }
            _start(_args);
        }

        private static IntPtr _servStatusHandle;

        private static ServiceStatus _servStatus;

        private static readonly ServiceCtrlHandlerProcEx ServCtrlHandlerProc = BaseServiceControlHandler;

        private static int BaseServiceControlHandler(ServiceControlType opcode, int eventType, IntPtr eventData, IntPtr context)
        {
            switch (opcode)
            {
                case ServiceControlType.ServiceControlStop:
                    _servStatus.dwWin32ExitCode = 0;
                    _servStatus.dwCurrentState = ServiceCurrentStateType.ServiceStopped;
                    _servStatus.dwCheckPoint = 0;
                    _servStatus.dwWaitHint = 0;
                    SetServiceStatus(_servStatusHandle, ref _servStatus);
                    _stop();
                    break;
            }
            return 0; //NO_ERROR
        }
        #endregion

        #region ServiceApi
        ///<summary>A collection of Win32 API functions and structs for use with Win32 services.</summary>
        public static class ServicesApi
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
            internal static extern int SetServiceStatus(IntPtr hServiceStatus, ref ServiceStatus lpServiceStatus);

            /// <summary>
            /// Connects the main thread of a service process to the service control manager, which causes the thread to be the service control dispatcher thread for the calling process.
            /// </summary>
            /// <param name="lpServiceStartTable">A pointer to an array of SERVICE_TABLE_ENTRY structures containing one entry for each service that can execute in the calling process. The members of the last entry in the table must have NULL values to designate the end of the table.</param>
            /// <returns>If the function succeeds, the return value is nonzero.</returns>
            [DllImport("advapi32.dll", EntryPoint = "StartServiceCtrlDispatcher", SetLastError = true)]
            internal static extern int StartServiceCtrlDispatcher(ServiceTableEntry[] lpServiceStartTable);

            public const int ErrorFailedServiceControllerConnect = 1063;

            public const int ErrorInvalidData = 13;

            public const int ErrorServiceAlreadyRunning = 1057;

            public const int ServiceNoChange = 0xFFFF;

            [Flags]
            public enum ServiceType
            {
                /// <summary>Driver service.</summary>
                ServiceKernelDriver = 0x1,
                /// <summary>File system driver service.</summary>
                ServiceFileSystemDriver = 0x2,
                /// <summary>Service that runs in its own process.</summary>
                ServiceWin32OwnProcess = 0x10,
                /// <summary>Service that shares a process with one or more other services. </summary>
                ServiceWin32ShareProcess = 0x20,
                /// <summary>The service can interact with the desktop.</summary>
                ServiceInteractiveProcess = 0x100,

                ServicetypeNoChange = ServiceNoChange
            }

            /// <summary>
            /// Notifies a service of device events. (The service must have registered to receive these notifications using the RegisterDeviceNotification function.)
            /// </summary>
            public enum ServiceControlDeviceeventControl
            {
                /// <summary>system detected a new device</summary>
                DbtDevicearrival = 0x8000,
                /// <summary>wants to remove, may fail</summary>
                DbtDevicequeryremove = 0x8001,
                /// <summary>removal aborted</summary>
                DbtDevicequeryremovefailed = 0x8002,
                /// <summary>about to remove, still avail.</summary>
                DbtDeviceremovepending = 0x8003,
                /// <summary>device is gone</summary>
                DbtDeviceremovecomplete = 0x8004,
                /// <summary>type specific event</summary>
                DbtDevicetypespecific = 0x8005,
                /// <summary>user-defined event</summary>
                DbtCustomevent = 0x8006,
            }

            /// <summary>
            /// Notifies a service that the computer's hardware profile has changed.
            /// </summary>
            public enum ServiceControlHardwareprofilechangeControl
            {
                /// <summary>sent when a config has changed</summary>
                DbtConfigchanged = 0x0018,
                /// <summary>sent to ask if a config change is allowed</summary>
                DbtQuerychangeconfig = 0x0017,
                /// <summary>someone cancelled the config change</summary>
                DbtConfigchangecanceled = 0x0019
            }

            /// <summary>
            /// The power-management event.
            /// </summary>
            public enum ServiceControlPowereventControl
            {
                /// <summary>
                /// Requested to suspend the computer. 
                /// </summary>
                PbtApmquerysuspend = 0x0000,
                /// <summary>
                /// Requested to suspend the computer
                /// </summary>
                PbtApmquerystandby = 0x0001,
                /// <summary>
                /// permission to suspend the computer was denied.
                /// </summary>
                PbtApmquerysuspendfailed = 0x0002,
                /// <summary>
                /// permission to standby the computer was denied
                /// </summary>
                PbtApmquerystandbyfailed = 0x0003,
                /// <summary>
                /// the computer is about to enter a suspended state
                /// <para>An application should process this event by completing all tasks necessary to save data. This event may also be broadcast, without a prior PBT_APMQUERYSUSPEND event, if an application or device driver uses the SetSystemPowerState function to force suspension.</para>
                /// <para>The system allows approximately two seconds for an application to handle this notification. If an application is still performing operations after its time allotment has expired, the system may interrupt the application.</para>
                /// <para>Windows Server 2003 and Windows XP:  Applications are allowed up to 20 seconds to respond to the PBT_APMSUSPEND event.</para>
                /// </summary>
                PbtApmsuspend = 0x0004,
                /// <summary>
                /// the computer is about to enter the standby state
                /// </summary>
                PbtApmstandby = 0x0005,
                /// <summary>
                /// Notifies applications that the system has resumed operation. This event can indicate that some or all applications did not receive a PBT_APMSUSPEND event. For example, this event can be broadcast after a critical suspension caused by a failing battery.
                /// </summary>
                PbtApmresumecritical = 0x0006,
                /// <summary>
                /// Operation is resuming from a low-power state. This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key.
                /// <para>An application can receive this event only if it received the PBT_APMSUSPEND event before the computer was suspended. Otherwise, the application will receive a PBT_APMRESUMECRITICAL event.</para>
                /// <para>If the system wakes due to user activity (such as pressing the power button) or if the system detects user interaction at the physical console (such as mouse or keyboard input) after waking unattended, the system first broadcasts the PBT_APMRESUMEAUTOMATIC event, then it broadcasts the PBT_APMRESUMESUSPEND event. In addition, the system turns on the display. Your application should reopen files that it closed when the system entered sleep and prepare for user input.</para>
                /// <para>If the system wakes due to an external wake signal (remote wake), the system broadcasts only the PBT_APMRESUMEAUTOMATIC event. The PBT_APMRESUMESUSPEND event is not sent.</para>
                /// <para>Windows Server 2003 and Windows XP:  If an application called SetSystemPowerState with fForce set to TRUE or the system performed a critical suspend, the system will broadcast a PBT_APMRESUMECRITICAL event after waking.</para>
                /// </summary>
                PbtApmresumesuspend = 0x0007,
                /// <summary>
                /// Operation is resuming from a standby
                /// </summary>
                PbtApmresumestandby = 0x0008,
                /// <summary>
                /// indicates that a suspend operation failed after the PBT_APMSUSPEND event was broadcast.
                /// </summary>
                PbtfApmresumefromfailure = 0x00000001,
                /// <summary>
                /// Notifies applications that the battery power is low.
                /// </summary>
                PbtApmbatterylow = 0x0009,
                /// <summary>
                /// Power status has changed.
                /// <para>An application should process this event by calling the GetSystemPowerStatus function to retrieve the current power status of the computer. In particular, the application should check the ACLineStatus, BatteryFlag, BatteryLifeTime, and BatteryLifePercent members of the SYSTEM_POWER_STATUS structure for any changes. This event can occur when battery life drops to less than 5 minutes, or when the percentage of battery life drops below 10 percent, or if the battery life changes by 3 percent.</para>
                /// </summary>
                PbtApmpowerstatuschange = 0x000A,
                /// <summary>
                /// Notifies applications that the APM BIOS has signaled an APM OEM event.
                /// </summary>
                PbtApmoemevent = 0x000B,
                /// <summary>
                /// Operation is resuming automatically from a low-power state. This message is sent every time the system resumes.
                /// <para>If the system detects any user activity after broadcasting PBT_APMRESUMEAUTOMATIC, it will broadcast a PBT_APMRESUMESUSPEND event to let applications know they can resume full interaction with the user.</para>
                /// </summary>
                PbtApmresumeautomatic = 0x0012,
                /// <summary>
                /// A power setting change event has been received.
                /// </summary>
                PbtPowersettingchange = 0x8013
            }

            public enum ServiceControlSessionchangeControl
            {
                WtsConsoleConnect = 0x1,
                WtsConsoleDisconnect = 0x2,
                WtsRemoteConnect = 0x3,
                WtsRemoteDisconnect = 0x4,
                WtsSessionLogon = 0x5,
                WtsSessionLogoff = 0x6,
                WtsSessionLock = 0x7,
                WtsSessionUnlock = 0x8,
                WtsSessionRemoteControl = 0x9,
                WtsSessionCreate = 0xa,
                WtsSessionTerminate = 0xb
            }

            public enum ServiceControlType
            {
                /// <summary>
                /// Notifies a service that it should stop.
                /// <para>If a service accepts this control code, it must stop upon receipt and return NO_ERROR. After the SCM sends this control code, it will not send other control codes to the service.</para>
                /// <para>Windows XP:  If the service returns NO_ERROR and continues to run, it continues to receive control codes. This behavior changed starting with Windows Server 2003 and Windows XP with SP2.</para>
                /// </summary>
                ServiceControlStop = 0x00000001,
                /// <summary>
                /// Notifies a service that it should pause.
                /// </summary>
                ServiceControlPause = 0x00000002,
                /// <summary>Notifies a paused service that it should resume.</summary>
                ServiceControlContinue = 0x00000003,
                /// <summary>Notifies a service to report its current status information to the service control manager.
                /// <para>The handler should simply return NO_ERROR; the SCM is aware of the current state of the service.</para>
                /// </summary>
                ServiceControlInterrogate = 0x00000004,
                /// <summary>
                /// Notifies a service that the system is shutting down so the service can perform cleanup tasks. Note that services that register for SERVICE_CONTROL_PRESHUTDOWN notifications cannot receive this notification because they have already stopped.
                /// <para>If a service accepts this control code, it must stop after it performs its cleanup tasks and return NO_ERROR. After the SCM sends this control code, it will not send other control codes to the service.</para>
                /// </summary>
                ServiceControlShutdown = 0x00000005,
                /// <summary>
                /// Notifies a service that service-specific startup parameters have changed. The service should reread its startup parameters.
                /// </summary>
                ServiceControlParamchange = 0x00000006,
                /// <summary>
                /// Notifies a network service that there is a new component for binding. The service should bind to the new component.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                ServiceControlNetbindadd = 0x00000007,
                /// <summary>
                /// Notifies a network service that a component for binding has been removed. The service should reread its binding information and unbind from the removed component.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                ServiceControlNetbindremove = 0x00000008,
                /// <summary>
                /// Notifies a network service that a disabled binding has been enabled. The service should reread its binding information and add the new binding.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                ServiceControlNetbindenable = 0x00000009,
                /// <summary>
                /// Notifies a network service that one of its bindings has been disabled. The service should reread its binding information and remove the binding.
                /// <para>Applications should use Plug and Play functionality instead.</para>
                /// </summary>
                ServiceControlNetbinddisable = 0x0000000A,
                /// <summary>
                /// Notifies a service of device events. (The service must have registered to receive these notifications using the RegisterDeviceNotification function.) The dwEventType and lpEventData parameters contain additional information.
                /// </summary>
                ServiceControlDeviceevent = 0x0000000B,
                /// <summary>
                /// Notifies a service that the computer's hardware profile has changed. The dwEventType parameter contains additional information.
                /// </summary>
                ServiceControlHardwareprofilechange = 0x0000000C,
                /// <summary>
                /// Notifies a service of system power events. The dwEventType parameter contains additional information. If dwEventType is PBT_POWERSETTINGCHANGE, the lpEventData parameter also contains additional information.
                /// </summary>
                ServiceControlPowerevent = 0x0000000D,
                /// <summary>
                /// Notifies a service of session change events. Note that a service will only be notified of a user logon if it is fully loaded before the logon attempt is made. The dwEventType and lpEventData parameters contain additional information.
                /// </summary>
                ServiceControlSessionchange = 0x0000000E,
                /// <summary>
                /// Notifies a service that the system will be shutting down. Services that need additional time to perform cleanup tasks beyond the tight time restriction at system shutdown can use this notification. The service control manager sends this notification to applications that have registered for it before sending a SERVICE_CONTROL_SHUTDOWN notification to applications that have registered for that notification.
                /// <para>A service that handles this notification blocks system shutdown until the service stops or the preshutdown time-out interval specified through SERVICE_PRESHUTDOWN_INFO expires. Because this affects the user experience, services should use this feature only if it is absolutely necessary to avoid data loss or significant recovery time at the next system start.</para>
                /// <para>Windows Server 2003 and Windows XP:  This value is not supported.</para>
                /// </summary>
                ServiceControlPreshutdown = 0x0000000F,
                /// <summary>
                /// Notifies a service that the system time has changed. The lpEventData parameter contains additional information. The dwEventType parameter is not used.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                ServiceControlTimechange = 0x00000010,
                /// <summary>
                /// Notifies a service registered for a service trigger event that the event has occurred.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                ServiceControlTriggerevent = 0x00000020
            }

            public enum ServiceCurrentStateType
            {
                ServiceStopped = 0x00000001,
                ServiceStartPending = 0x00000002,
                ServiceStopPending = 0x00000003,
                ServiceRunning = 0x00000004,
                ServiceContinuePending = 0x00000005,
                ServicePausePending = 0x00000006,
                ServicePaused = 0x00000007
            }

            /// <summary>
            /// The control codes the service accepts and processes in its handler function (see Handler and HandlerEx). 
            /// A user interface process can control a service by specifying a control command in the ControlService or ControlServiceEx function. 
            /// By default, all services accept the SERVICE_CONTROL_INTERROGATE value. To accept the SERVICE_CONTROL_DEVICEEVENT value, the service 
            /// must register to receive device events by using the RegisterDeviceNotification function.
            /// </summary>
            [Flags]
            public enum ControlsAccepted
            {
                ServiceAcceptAll = ServiceAcceptStop | ServiceAcceptPauseContinue | ServiceAcceptShutdown | ServiceAcceptParamchange | ServiceAcceptNetbindchange |
                                      ServiceAcceptHardwareprofilechange | ServiceAcceptPowerevent | ServiceAcceptSessionchange | ServiceAcceptPreshutdown | ServiceAcceptTimechange |
                                      ServiceAcceptTriggerevent,
                /// <summary>
                /// The service can be stopped.
                /// </summary>
                ServiceAcceptStop = 0x00000001,
                /// <summary>
                /// The service can be paused and continued.
                /// </summary>
                ServiceAcceptPauseContinue = 0x00000002,
                /// <summary>
                /// The service is notified when system shutdown occurs.
                /// </summary>
                ServiceAcceptShutdown = 0x00000004,
                /// <summary>
                /// The service can reread its startup parameters without being stopped and restarted.
                /// </summary>
                ServiceAcceptParamchange = 0x00000008,
                /// <summary>
                /// The service is a network component that can accept changes in its binding without being stopped and restarted.
                /// </summary>
                ServiceAcceptNetbindchange = 0x00000010,
                /// <summary>
                /// The service is notified when the computer's hardware profile has changed. This enables the system to send SERVICE_CONTROL_HARDWAREPROFILECHANGE notifications to the service.
                /// </summary>
                ServiceAcceptHardwareprofilechange = 0x00000020,
                /// <summary>
                /// The service is notified when the computer's power status has changed. This enables the system to send SERVICE_CONTROL_POWEREVENT notifications to the service.
                /// </summary>
                ServiceAcceptPowerevent = 0x00000040,
                /// <summary>
                /// The service is notified when the computer's session status has changed. This enables the system to send SERVICE_CONTROL_SESSIONCHANGE notifications to the service.
                /// </summary>
                ServiceAcceptSessionchange = 0x00000080,
                /// <summary>
                /// The service can perform preshutdown tasks.
                /// </summary>
                ServiceAcceptPreshutdown = 0x00000100,
                /// <summary>
                /// The service is notified when the system time has changed. This enables the system to send SERVICE_CONTROL_TIMECHANGE notifications to the service.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                ServiceAcceptTimechange = 0x00000200,
                /// <summary>
                /// The service is notified when an event for which the service has registered occurs. This enables the system to send SERVICE_CONTROL_TRIGGEREVENT notifications to the service.
                /// <para>Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                ServiceAcceptTriggerevent = 0x00000400,
                /// <summary>
                /// The services is notified when the user initiates a reboot.
                /// <para>Windows Server 2008 R2, Windows 7, Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This control code is not supported.</para>
                /// </summary>
                ServiceAcceptUsermodereboot = 0x00000800
            }

            public enum NetBindControl
            {
                Netbindadd,
                Netbinddisable,
                Netbindenable,
                Netbindremove
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct PowerbroadcastSetting
            {
                public Guid PowerSetting;
                public Int32 DataLength;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct WtssessionNotification
            {
                public uint cbSize;
                public uint dwSessionId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct ServiceTimechangeInfo
            {
                public long liNewTime;
                public long liOldTime;

                public DateTime LiNewTimeToDateTime()
                {
                    return DateTime.FromFileTime(liNewTime);
                }

                public DateTime LiOldTimeToDateTime()
                {
                    return DateTime.FromFileTime(liOldTime);
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct ServiceStatus
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

            public struct ServiceTableEntry
            {
                public string LpServiceName;
                [MarshalAs(UnmanagedType.FunctionPtr)]
                public ServiceMainProc LpServiceProc;
            }
        }
        #endregion

        #region PlatformApi
        private static bool? _iamWindowsAndIamService;

        public static bool IamNtService
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
                return IamWindows && !IamNtService;
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

        #region Console / unix
        public static int RunConsole()
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += (s) =>
            {
                _onMessage("Assembly.LoadContextUnloading");
                __stop();
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                _onMessage("CurrentDomain.ProcessExit");
                __stop();
            };
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _onMessage("Console.CancelKeyPressCancelKeyPress");
                __stop();
            };
            if (IamWindows)
                SetConsoleCtrlHandler(ConsoleCtrlCheck, true);
            _start(_args);
            return 0;
        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            _onMessage("ConsoleCtrlCheck:" + ctrlType.ToString());
            __stop();
            return true;
        }

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

        public delegate bool HandlerRoutine(CtrlTypes ctrlType);

        public enum CtrlTypes
        {
            CtrlCEvent = 0,
            CtrlBreakEvent,
            CtrlCloseEvent,
            CtrlLogoffEvent = 5,
            CtrlShutdownEvent
        }
        #endregion
    }
}
