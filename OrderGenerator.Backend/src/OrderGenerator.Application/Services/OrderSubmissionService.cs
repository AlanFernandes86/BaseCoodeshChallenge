using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Domain.Services;
using Microsoft.Extensions.Logging;

namespace OrderGenerator.Application.Services
{
    /// <summary>
    /// Represents an order queued in the outbox when the FIX session is offline
    /// </summary>
    public class OutboxOrder
    {
        public string Symbol { get; set; } = "";
        public OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string ClOrdID { get; set; } = "";
        public string ConnectionId { get; set; } = "";
    }

    /// <summary>
    /// Service for submitting orders via FIX protocol.
    /// Handles order validation, queuing when offline, and submission to Accumulator.
    /// </summary>
    public class OrderSubmissionService
    {
        private readonly IFIXOrderSubmissionService _fixService;
        private readonly ILogger<OrderSubmissionService> _logger;
        private readonly object _idLock = new object();
        private long _orderCounter = 0;

        // In-memory Outbox queue for orders submitted while the FIX session is offline
        private readonly ConcurrentQueue<OutboxOrder> _outboxQueue = new();

        // Tracks pending orders: ClOrdID -> (TCS, ConnectionId)
        private readonly ConcurrentDictionary<string, (TaskCompletionSource<OrderSubmissionResult> TCS, string ConnectionId)> _pendingOrders = new();

        /// <summary>
        /// Event fired when any order finishes processing (accepted, rejected or failed)
        /// </summary>
        public event EventHandler<OrderSubmissionResult>? OrderProcessed;

        public ConcurrentQueue<OutboxOrder> OutboxQueue => _outboxQueue;

        public OrderSubmissionService(IFIXOrderSubmissionService fixService, ILogger<OrderSubmissionService> logger)
        {
            _fixService = fixService ?? throw new ArgumentNullException(nameof(fixService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fixService.ExecutionReportReceived += OnExecutionReportReceived;
        }

        private void OnExecutionReportReceived(object? sender, ExecutionReportData report)
        {
            if (_pendingOrders.TryRemove(report.ClOrdID, out var state))
            {
                var result = new OrderSubmissionResult
                {
                    ClOrdID = report.ClOrdID,
                    OrderID = report.OrderID,
                    IsAccepted = report.IsAccepted,
                    ExecType = report.ExecType,
                    OrdStatus = report.OrdStatus,
                    Message = report.Text,
                    ReceivedAt = report.ReceivedAt,
                    ConnectionId = state.ConnectionId
                };

                state.TCS.TrySetResult(result);
                
                // Fire event for external listeners (e.g. SignalR notifier in WebAPI)
                OrderProcessed?.Invoke(this, result);
            }
        }

        /// <summary>
        /// Submit an order to the Accumulator via FIX.
        /// If the FIX session is offline, queues the order in the Outbox and returns a PENDING result immediately.
        /// </summary>
        public async Task<OrderSubmissionResult> SubmitOrderAsync(
            string symbol,
            OrderSide side,
            decimal quantity,
            decimal price,
            string connectionId = "",
            int timeoutMs = 15000)
        {
            // Generate unique order ID
            string clOrdID = GenerateClientOrderID();

            // If the FIX session is offline, queue the order in the Outbox
            if (!_fixService.IsSessionActive)
            {
                var outboxOrder = new OutboxOrder
                {
                    Symbol = symbol,
                    Side = side,
                    Quantity = quantity,
                    Price = price,
                    ClOrdID = clOrdID,
                    ConnectionId = connectionId
                };

                _outboxQueue.Enqueue(outboxOrder);

                _logger.LogInformation($"[SUBMIT-OUTBOX] Session offline. Queued order {clOrdID} for {symbol}");

                return new OrderSubmissionResult
                {
                    ClOrdID = clOrdID,
                    IsAccepted = false,
                    OrdStatus = "PENDING",
                    Message = "A ordem foi enfileirada pois o servidor de exposição está offline. Será processada assim que a conexão for estabelecida.",
                    ReceivedAt = DateTime.UtcNow,
                    ConnectionId = connectionId
                };
            }

            // Session is active: process immediately
            var tcs = new TaskCompletionSource<OrderSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingOrders.TryAdd(clOrdID, (tcs, connectionId)))
            {
                return OrderSubmissionResult.CreateRejected(clOrdID, "Duplicate client order ID");
            }

            try
            {
                // Send NewOrderSingle via FIX
                _fixService.SendNewOrderSingle(symbol, side, quantity, price, clOrdID);

                // Wait for the TaskCompletionSource to complete or timeout
                var delayTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(tcs.Task, delayTask);

                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    _pendingOrders.TryRemove(clOrdID, out _);
                    return OrderSubmissionResult.CreateRejected(
                        clOrdID,
                        $"No response from Accumulator (timeout after {timeoutMs}ms)"
                    );
                }
            }
            catch (Exception ex)
            {
                _pendingOrders.TryRemove(clOrdID, out _);
                return OrderSubmissionResult.CreateRejected(
                    clOrdID,
                    $"FIX submission error: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Sends a queued order from the Outbox via FIX and registers it for responses
        /// </summary>
        public void SendOutboxOrder(OutboxOrder outboxOrder)
        {
            var tcs = new TaskCompletionSource<OrderSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            // Register so when the ExecutionReport is received, we complete the TCS and fire the processed event
            _pendingOrders.TryAdd(outboxOrder.ClOrdID, (tcs, outboxOrder.ConnectionId));
            
            _logger.LogInformation($"[SUBMIT-OUTBOX] Despachando ordem enfileirada {outboxOrder.ClOrdID} via FIX...");
            
            _fixService.SendNewOrderSingle(
                outboxOrder.Symbol,
                outboxOrder.Side,
                outboxOrder.Quantity,
                outboxOrder.Price,
                outboxOrder.ClOrdID
            );
        }

        /// <summary>
        /// Generate unique client order ID
        /// </summary>
        private string GenerateClientOrderID()
        {
            lock (_idLock)
            {
                _orderCounter++;
                return $"CLT-{DateTime.UtcNow:yyyyMMdd}-{_orderCounter:D6}";
            }
        }
    }

    /// <summary>
    /// Result of order submission attempt
    /// </summary>
    public class OrderSubmissionResult
    {
        public string ClOrdID { get; set; } = "";
        public string OrderID { get; set; } = "";
        public bool IsAccepted { get; set; }
        public string ExecType { get; set; } = "";
        public string OrdStatus { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
        public string ConnectionId { get; set; } = "";

        public static OrderSubmissionResult CreateRejected(string clOrdID, string message)
        {
            return new OrderSubmissionResult
            {
                ClOrdID = clOrdID,
                IsAccepted = false,
                Message = message,
                ReceivedAt = DateTime.UtcNow,
                OrdStatus = "8"  // REJECTED
            };
        }
    }
}
