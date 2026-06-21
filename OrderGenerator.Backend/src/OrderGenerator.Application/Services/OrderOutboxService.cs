using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderGenerator.Domain.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using OrderGenerator.Application.DTOs;

namespace OrderGenerator.Application.Services
{
    /// <summary>
    /// Background worker that drains the Outbox Queue when the FIX session is active
    /// and pushes real-time SignalR notifications to clients when enqueued orders are processed.
    /// </summary>
    public class OrderOutboxService : BackgroundService
    {
        private readonly IOrderSubmissionService _submissionService;
        private readonly IOrderTracker _orderTracker;
        private readonly ILogger<OrderOutboxService> _logger;

        public OrderOutboxService(
            IOrderSubmissionService submissionService,
            IOrderTracker orderTracker,
            ILogger<OrderOutboxService> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[OUTBOX-WORKER] Starting Outbox Queue Worker...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_submissionService.IsReady && _orderTracker.OutboxQueue.TryDequeue(out var pendingOrder))
                    {
                        _logger.LogInformation($"[OUTBOX-WORKER] Dequeued order {pendingOrder.ClOrdID}. Despachando para Accumulator via FIX.");
                        
                        _submissionService.SendNewOrderSingle(
                            pendingOrder.Symbol,
                            pendingOrder.Side,
                            pendingOrder.Quantity,
                            pendingOrder.Price,
                            pendingOrder.ClOrdID
                            );
                    }
                    else
                    {
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
