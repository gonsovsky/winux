using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace winux
{
    public class WebService : IHostedService, IDisposable
    {
        private Timer _timer;

        private static List<string> Msgs = new List<string>();

        private static ILogger<WebService> _logger;

        public static void Message(string message)
        {
            Msgs.Add(DateTime.Now.ToString() + ":" + message);
            try
            {
                _logger.LogInformation(message);
            }
            catch (Exception)
            {

            }

            if (PlatformTools.IamWindowsAndIamService())
            {
                try
                {
                    System.IO.File.AppendAllText("C:\\_log.txt", message+"\r\n");
                }
                catch (Exception)
                {
                }
            }
   
        }

        public WebService(IApplicationLifetime applicationLifetime, ILogger<WebService> logger)
        {
            _logger = logger;
            if (PlatformTools.IamWindowsAndIamService())
            {
                try
                {
                    System.IO.File.Delete("C:\\_log.txt");
                }
                catch (Exception)
                {
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
           SimpleListenerExample();
            _timer = new Timer(
                (e) =>  Message("United Win/Linux Web Service: I'm tick: " + DateTime.Now.ToString()),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Message("United Win/Linux Web Service: I'm stopping async..");
            
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private static Task t;

        public static void SimpleListenerExample()
        {
           t = new Task(() =>
                {
                    try
                    {
                        var prefixes = new string[] { "http://*:8080/" };
                        // URI prefixes are required,
                        // for example "http://*:8080".
                        if (prefixes == null || prefixes.Length == 0)
                            throw new ArgumentException("prefixes");

                        // Create a listener.
                        HttpListener listener = new HttpListener();
                        // Add the prefixes.
                        foreach (string s in prefixes)
                        {
                            listener.Prefixes.Add(s);
                        }

                        listener.Start();
                
                        Console.WriteLine("Listening...");
                        while (true)
                        {
                            ThreadPool.QueueUserWorkItem(Process, listener.GetContext());
                        }
                        
                    }
                    catch (Exception e)
                    {
                        Message(e.Message);
                    }
                  
                }
            );
            t.Start();
        }

        static void Process(object o)
        {
            var context = o as HttpListenerContext;
            // Note: The GetContext method blocks while waiting for a request.
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            // Construct a response.
            string responseString = string.Join("\r\n", Msgs);
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
        }
    }
}
