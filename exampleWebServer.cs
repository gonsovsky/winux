using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Winux
{
    public static class ExampleWebServer
    {
        static CancellationTokenSource tkn = new CancellationTokenSource();
        public static void Start()
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            Log= Path.GetDirectoryName(path) + "/log.txt";
            if (File.Exists(Log))
                File.Delete(Log);
            Message("Web is started http://localhost:8081. Log file: " + Log);

            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://+:{0}/", 8081));
            Serve().Wait();
        }

        public async static Task Serve()
        {
            _listener.Start();

            while (true)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();

                    ExampleWebServer.Message("Received request: " + ctx.Request.RawUrl + " from " + ctx.Request.UserHostAddress);

                    ctx.Response.Headers.Add("content-type: text/plain; charset=UTF-8");
                    ctx.Response.Headers.Add("x-content-type-options: nosniff");
                    ctx.Response.Headers.Add("x-xss-protection:1; mode=block");
                    ctx.Response.Headers.Add("x-frame-options:DENY");
                    ctx.Response.Headers.Add("cache-control:no-store, no-cache, must-revalidate");
                    ctx.Response.Headers.Add("pragma:no-cache");
                    ctx.Response.Headers.Add("Server", "jl");

                    var response = string.Join("\r\n\r\n", ExampleWebServer._msgs);
                    using (var sw = new StreamWriter(ctx.Response.OutputStream))
                    {
                        await sw.WriteAsync(response);
                        await sw.FlushAsync();
                    }
                }
                catch {
                    break;
                };
                
            }
            Message("Socket server closed");
            Task.Delay(500).Wait();
        }
            

        public static void Stop()
        {
            Message("Linux SIGTERM || Windows SCM Stop Command || Console break requested.");
            Task.Delay(500).Wait();
            Message("Graceful Shutdown and sleep for 1 seconds...");

            Console.Out.WriteLine(
                "Stopping server...");
            try
            {

                    _listener.Stop();
                    _listener.Close();
          
            }
            catch{ };

            Task.Delay(500).Wait();
            Message("Everything is stopped");
            Task.Delay(500).Wait();
        }

        public static string Log;
      

        public static void Message(string message)
        {
            message = DateTime.Now + ":" + message;
            _msgs.Add(message);
            Console.WriteLine(message);
            File.AppendAllText(Log, message + "\r\n");
        }

        private static  List<string> _msgs = new List<string>();

        private static  HttpListener _listener;

    }
}
