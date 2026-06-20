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

        public OrderSubmissionService(IFIXOrderSubmissionService fixService)
        {
            _fixService = fixService ?? throw new ArgumentNullException(nameof(fixService));
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
            int timeoutMs = 5000)
        {
            // Generate unique order ID
            string clOrdID = GenerateClientOrderID();

            try
            {
                // Send NewOrderSingle via FIX
                _fixService.SendNewOrderSingle(symbol, side, quantity, price, clOrdID);

                // Wait for ExecutionReport response
                var result = await WaitForExecutionReportAsync(clOrdID, timeoutMs);

                return result;
            }
            catch (Exception ex)
            {
                return OrderSubmissionResult.CreateRejected(
                    clOrdID,
                    $"FIX submission error: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Wait for ExecutionReport response for a given ClOrdID
        /// </summary>
        private async Task<OrderSubmissionResult> WaitForExecutionReportAsync(
            string clOrdID,
            int timeoutMs)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (_fixService.TryGetExecutionReport(out var report))
                {
                    if (report?.ClOrdID == clOrdID)
                    {
                        return new OrderSubmissionResult
                        {
                            ClOrdID = clOrdID,
                            OrderID = report.OrderID,
                            IsAccepted = report.IsAccepted,
                            ExecType = report.ExecType,
                            OrdStatus = report.OrdStatus,
                            Message = report.Text,
                            ReceivedAt = report.ReceivedAt
                        };
                    }
                    // Put back reports for other orders (shouldn't happen in this simplified version)
                }

                // Sleep briefly to avoid busy-waiting
                await Task.Delay(50);
            }

            return OrderSubmissionResult.CreateRejected(
                clOrdID,
                $"No response from Accumulator (timeout after {timeoutMs}ms)"
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

