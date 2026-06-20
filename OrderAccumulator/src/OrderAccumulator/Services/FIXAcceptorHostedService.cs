using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Store;

namespace OrderAccumulator.Services
{
    /// <summary>
    /// Hosted service that encapsulates the lifecycle of the QuickFIX/n ThreadedSocketAcceptor.
    /// Manages graceful socket shutdown on container stop.
    /// </summary>
    public class FIXAcceptorHostedService : IHostedService
    {
        private readonly IApplication _application;
        private readonly SessionSettings _settings;
        private readonly ILogger<FIXAcceptorHostedService> _logger;
        private ThreadedSocketAcceptor? _acceptor;

        public FIXAcceptorHostedService(
            IApplication application,
            SessionSettings settings,
            ILogger<FIXAcceptorHostedService> logger)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting FIX Acceptor Service...");
            try
            {
                // Create message store factory (persistent for session sequence numbers)
                var storeFactory = new FileStoreFactory(_settings);

                // Create the FIX acceptor (no explicit logger factory passed to QuickFIX)
                _acceptor = new ThreadedSocketAcceptor(_application, storeFactory, _settings, (QuickFix.Logger.ILogFactory)null!);
                
                _acceptor.Start();
                _logger.LogInformation("✅ FIX Acceptor started successfully on configured port");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to start FIX Acceptor: {Message}", ex.Message);
                throw;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FIX Acceptor Service graciosamente...");
            if (_acceptor != null)
            {
                try
                {
                    _acceptor.Stop();
                    _logger.LogInformation("✅ FIX Acceptor stopped gracefully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error stopping FIX Acceptor: {Message}", ex.Message);
                }
            }
            return Task.CompletedTask;
        }
    }
}
