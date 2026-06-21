using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Store;
using OrderAccumulator.Core;
using OrderAccumulator.Infrastructure.FIX;

namespace OrderAccumulator.Services
{
    /// <summary>
    /// Hosted service that manages the QuickFIX/n ThreadedSocketAcceptor lifecycle.
    ///
    /// Startup sequence (order matters):
    ///   1. Read the local FIX FileStore and reconstruct ExposureState from historical orders.
    ///      This completes BEFORE any new connection is accepted, so there is no race between
    ///      reconstruction and live order processing.
    ///   2. Start the ThreadedSocketAcceptor — now ready to accept new FIX connections.
    ///
    /// Shutdown: gracefully stops the acceptor on container stop.
    /// </summary>
    public class FIXAcceptorHostedService : IHostedService
    {
        private readonly IApplication _application;
        private readonly SessionSettings _settings;
        private readonly ExposureState _exposureState;
        private readonly FIXStoreExposureReconstructor _reconstructor;
        private readonly ILogger<FIXAcceptorHostedService> _logger;
        private ThreadedSocketAcceptor? _acceptor;

        public FIXAcceptorHostedService(
            IApplication application,
            SessionSettings settings,
            ExposureState exposureState,
            FIXStoreExposureReconstructor reconstructor,
            ILogger<FIXAcceptorHostedService> logger)
        {
            _application   = application   ?? throw new ArgumentNullException(nameof(application));
            _settings      = settings      ?? throw new ArgumentNullException(nameof(settings));
            _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
            _reconstructor = reconstructor ?? throw new ArgumentNullException(nameof(reconstructor));
            _logger        = logger        ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("FIX Acceptor Service starting...");

            _logger.LogInformation("1 - Reconstructing exposure state from local FIX FileStore...");
            try
            {
                int ordersReplayed = _reconstructor.Reconstruct();
                _logger.LogInformation("1 - Exposure state reconstructed from {Count} historical orders.", ordersReplayed);
                _exposureState.PrintState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "1 - ❌ Failed to reconstruct exposure state: {Message}", ex.Message);
                throw;
            }

            _logger.LogInformation("2 - Starting FIX Acceptor...");
            try
            {
                var storeFactory = new FileStoreFactory(_settings);
                _acceptor = new ThreadedSocketAcceptor(_application, storeFactory, _settings, (QuickFix.Logger.ILogFactory)null!);
                _acceptor.Start();
                _logger.LogInformation("2 - ✅ FIX Acceptor started successfully on configured port");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2 - ❌ Failed to start FIX Acceptor: {Message}", ex.Message);
                throw;
            }
                
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FIX Acceptor Service...");
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
