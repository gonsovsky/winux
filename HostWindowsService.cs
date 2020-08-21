using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace winux
{
    public class HostWindowsService : ServiceBase, IHostLifetime
    {
        private readonly ILogger<HostWindowsService> _logger;

        private readonly TaskCompletionSource<object> _delayStart = new TaskCompletionSource<object>();

        public HostWindowsService(IApplicationLifetime applicationLifetime, ILogger<HostWindowsService> logger)
        {
            _logger = logger;
            WebService.Message("Windows Service: I'm started as Windows service");
            ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        }

        private IApplicationLifetime ApplicationLifetime { get; }

        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _delayStart.TrySetCanceled());
            ApplicationLifetime.ApplicationStopping.Register(Stop);

            new Thread(Run).Start(); 
            return _delayStart.Task;
        }

        private void Run()
        {
            try
            {
                Run(this); 
                _delayStart.TrySetException(new InvalidOperationException("Windows Service: Stopped without starting"));
            }
            catch (Exception ex)
            {
                _delayStart.TrySetException(ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            WebService.Message("Windows Service: StopAsync method called.");
            Stop();
            return Task.CompletedTask;
        }

        protected override void OnStart(string[] args)
        {
            WebService.Message("Windows Service: OnStarted method called.");
            _delayStart.TrySetResult(null);
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            WebService.Message("Windows Service: OnStop method called.");
            ApplicationLifetime.StopApplication();
            base.OnStop();
        }
    }
}
