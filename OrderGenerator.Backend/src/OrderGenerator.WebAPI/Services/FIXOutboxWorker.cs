using OrderGenerator.Application.Services;
using Microsoft.AspNetCore.SignalR;
using OrderGenerator.WebAPI.Hubs;
using OrderGenerator.Domain.Services;

namespace OrderGenerator.WebAPI.Services
{
    /// <summary>
    /// Background worker that drains the Outbox Queue when the FIX session is active
    /// and pushes real-time SignalR notifications to clients when enqueued orders are processed.
    /// </summary>
    public class FIXOutboxWorker : BackgroundService
    {
        private readonly OrderSubmissionService _submissionService;
        private readonly IFIXOrderSubmissionService _fixInitiator;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ILogger<FIXOutboxWorker> _logger;

        public FIXOutboxWorker(
            OrderSubmissionService submissionService,
            IFIXOrderSubmissionService fixInitiator,
            IHubContext<OrderHub> hubContext,
            ILogger<FIXOutboxWorker> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _fixInitiator = fixInitiator ?? throw new ArgumentNullException(nameof(fixInitiator));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to the OrderProcessed event to capture async FIX execution reports
            _submissionService.OrderProcessed += OnOrderProcessed;
        }

        private async void OnOrderProcessed(object? sender, OrderSubmissionResult result)
        {
            // If the connectionId is set, push the result to the specific SignalR client
            if (!string.IsNullOrEmpty(result.ConnectionId))
            {
                try
                {
                    _logger.LogInformation($"[OUTBOX-WORKER] Pushing status update to client {result.ConnectionId} for order {result.ClOrdID} (Status={result.OrdStatus})");
                    
                    await _hubContext.Clients.Client(result.ConnectionId).SendAsync("OrderResult", new
                    {
                        success = result.IsAccepted,
                        status = result.OrdStatus == "8" ? "REJECTED" : result.IsAccepted ? "ACCEPTED" : result.OrdStatus,
                        orderId = result.OrderID,
                        clOrdId = result.ClOrdID,
                        message = result.Message,
                        timestamp = result.ReceivedAt
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[OUTBOX-WORKER] Failed to push update to client {result.ConnectionId}: {ex.Message}");
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[OUTBOX-WORKER] Starting Outbox Queue Worker...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if the FIX session is active and there are enqueued orders in the Outbox
                    if (_fixInitiator.IsSessionActive && _submissionService.OutboxQueue.TryDequeue(out var pendingOrder))
                    {
                        _logger.LogInformation($"[OUTBOX-WORKER] Dequeued order {pendingOrder.ClOrdID}. Despachando para Accumulator via FIX.");
                        
                        _submissionService.SendOutboxOrder(pendingOrder);
                        
                        // Sleep briefly between dispatches to prevent overwhelming the session
                        await Task.Delay(100, stoppingToken);
                    }
                    else
                    {
                        // Sleep briefly before checking again
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[OUTBOX-WORKER] Error processing queue: {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("[OUTBOX-WORKER] Outbox Queue Worker stopped.");
        }
    }
}
