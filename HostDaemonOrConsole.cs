using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;


namespace winux
{
    public class HostDaemonOrConsole : IHostedService
    {
        readonly IApplicationLifetime _appLifetime;
        readonly ILogger<HostDaemonOrConsole> _logger;
        IHostingEnvironment _environment;
        IConfiguration _configuration;

        public HostDaemonOrConsole(
            IConfiguration configuration,
            IHostingEnvironment environment,
            ILogger<HostDaemonOrConsole> logger,
            IApplicationLifetime appLifetime)
        {
            this._configuration = configuration;
            this._logger = logger;
            this._appLifetime = appLifetime;
            this._environment = environment;
            WebService.Message("Daemon or Console: I'm started as Console or Daemon");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            WebService.Message("Daemon or Console: StartAsync method called.");
            this._appLifetime.ApplicationStarted.Register(OnStarted);
            this._appLifetime.ApplicationStopping.Register(OnStopping);
            this._appLifetime.ApplicationStopped.Register(OnStopped);
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            WebService.Message("Daemon or Console: OnStarted method called.");
        }

        private void OnStopping()
        {
            WebService.Message("Daemon or Console: OnStopping method called.");
        }

        private void OnStopped()
        {
            WebService.Message("Daemon or Console: OnStopped method called.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            WebService.Message("Daemon or Console: StopAsync method called.");
            return Task.CompletedTask;
        }
    }
}