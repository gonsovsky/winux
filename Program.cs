namespace Winux
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return Winux.Run
                (
                    args,

                    (a) =>
                    {
                        ExampleWebServer.Start(a);
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