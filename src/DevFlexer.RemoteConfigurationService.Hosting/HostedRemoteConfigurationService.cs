using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public class HostedRemoteConfigurationService : IHostedService, IDisposable
    {
        private readonly ILogger<HostedRemoteConfigurationService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IRemoteConfigurationService _remoteConfigurationService;
        private Task _executingTask;
        private readonly CancellationTokenSource _cts = new();

        public HostedRemoteConfigurationService(ILogger<HostedRemoteConfigurationService> logger,
                                        IHostApplicationLifetime applicationLifetime,
                                        IRemoteConfigurationService remoteConfigurationService)
        {
            _logger = logger;
            _applicationLifetime = applicationLifetime;
            _remoteConfigurationService = remoteConfigurationService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Remote Configuration Service.");

            _applicationLifetime.ApplicationStarted.Register(OnStarted);
            _applicationLifetime.ApplicationStopping.Register(OnStopping);
            _applicationLifetime.ApplicationStopped.Register(OnStopped);

            _executingTask = ExecuteAsync(_cts.Token);

            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }

            try
            {
                // Signal cancellation to the executing method
                _cts.Cancel();
            }
            finally
            {
                // Wait until the task completes or the stop token triggers
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _remoteConfigurationService.InitializeAsync(cancellationToken);
            }
            //todo CancellationException은 제외하도록 하자.
            catch (Exception e)
            {
                _logger.LogError(e, "An unhandled exception occurred while attempting to initialize the configuration storage.");

                _logger.LogInformation("The application will be terminated.");

                await StopAsync(cancellationToken);

                _applicationLifetime.StopApplication();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }

        private void OnStarted()
        {
            _logger.LogInformation("Configuration Service started.");
        }

        private void OnStopping()
        {
            _logger.LogInformation("Configuration Service is stopping...");
        }

        private void OnStopped()
        {
            _logger.LogInformation("Configuration Service stopped.");
        }
    }
}
