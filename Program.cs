using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace winux
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddJsonFile($"appsettings.json", true);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);
                    configApp.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddHostedService<WebService>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddSerilog(new LoggerConfiguration()
                        .ReadFrom.Configuration(hostContext.Configuration)
                        .CreateLogger());
                    configLogging.AddConsole();
                    configLogging.AddDebug();
                });

            if (PlatformTools.IamWindowsAndIamService())
            {
                 await hostBuilder
                     .ConfigureServices((hostContext, services) => 
                         services.AddSingleton<IHostLifetime, HostWindowsService>())
                     .Build().RunAsync(default);
            }
            else
            {
                hostBuilder
                   .ConfigureServices((hostContext, services) =>
                    services.AddHostedService<HostDaemonOrConsole>());
                await hostBuilder.Build().RunAsync();
            }
        }
    }
}