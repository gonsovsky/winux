using System;



namespace Winux
{
    static class Program
    {
        static int Main(string[] args)
        {
            return Winux.Run
                (
                    () =>
                    {
                        ExampleWebServer.Start();
                    }

                    ,
                    () =>
                    {
                        ExampleWebServer.Stop();
                    }
                      
                    ,
                    (message) =>
                    {
                        ExampleWebServer.Message(message);
                    }
                );
        }
    }
}