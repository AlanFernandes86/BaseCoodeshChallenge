using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Domain.Services;

namespace OrderGenerator.Application.UseCases
{
    /// <summary>
    /// Service for submitting orders via FIX protocol
    /// Handles order validation and submission to Accumulator
    /// </summary>
    public class OrderSubmissionService
    {
        private readonly IFIXOrderSubmissionService _fixService;
        private readonly object _idLock = new object();
        private long _orderCounter = 0;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<OrderSubmissionResult>> _pendingOrders = new();

        public OrderSubmissionService(IFIXOrderSubmissionService fixService)
        {
            _fixService = fixService ?? throw new ArgumentNullException(nameof(fixService));
            _fixService.ExecutionReportReceived += OnExecutionReportReceived;
        }

        private void OnExecutionReportReceived(object? sender, ExecutionReportData report)
        {
            if (_pendingOrders.TryRemove(report.ClOrdID, out var tcs))
            {
                tcs.TrySetResult(new OrderSubmissionResult
                {
                    ClOrdID = report.ClOrdID,
                    OrderID = report.OrderID,
                    IsAccepted = report.IsAccepted,
                    ExecType = report.ExecType,
                    OrdStatus = report.OrdStatus,
                    Message = report.Text,
                    ReceivedAt = report.ReceivedAt
                });
            }
        }

        /// <summary>
        /// Submit an order to the Accumulator via FIX
        /// Returns true if accepted, false if rejected
        /// </summary>
        public async Task<OrderSubmissionResult> SubmitOrderAsync(
            string symbol,
            OrderSide side,
            decimal quantity,
            decimal price,
            int timeoutMs = 15000)  // Increased from 5000 to 15000 for Docker network latency
        {
            // Generate unique order ID
            string clOrdID = GenerateClientOrderID();
            var tcs = new TaskCompletionSource<OrderSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingOrders.TryAdd(clOrdID, tcs))
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

