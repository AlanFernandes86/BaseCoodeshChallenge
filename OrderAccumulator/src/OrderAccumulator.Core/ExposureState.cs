using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OrderAccumulator.Core.Models;

namespace OrderAccumulator.Core
{
    /// <summary>
    /// Pure domain logic for exposure tracking and order processing.
    /// Thread-safe, O(1) lookup, zero abstractions.
    /// 
    /// Responsibility: 
    ///   1. Track cumulative exposure per symbol
    ///   2. Accept/reject orders based on exposure limit (±R$ 100M)
    ///   3. Provide state snapshot for logging/monitoring
    /// 
    /// NOT responsible for:
    ///   - Validation (trust input is well-formed)
    ///   - FIX protocol details (that's FIXAcceptorApplication's job)
    ///   - Persistence (that's FIX FileStore's job)
    /// </summary>
    public class ExposureState
    {
        private readonly ConcurrentDictionary<string, decimal> _exposures;
        private readonly object _orderIdLock = new();
        private long _orderIdCounter = 0;

        public ExposureState()
        {
            _exposures = new ConcurrentDictionary<string, decimal>();
        }

        /// <summary>
        /// Process an order: check if new exposure would exceed limit, then update state.
        /// Thread-safe via ConcurrentDictionary atomic operations.
        /// </summary>
        public ProcessingResult TryProcessOrder(OrderInfo orderInfo)
        {
            // Calculate exposure impact
            decimal impact = orderInfo.CalculateExposureImpact();

            // Get current exposure for this symbol (default 0 if new)
            decimal currentExposure = _exposures.TryGetValue(orderInfo.Symbol, out var exp) ? exp : 0m;

            // Calculate what exposure would be AFTER this order
            decimal newExposure = currentExposure + impact;

            // Check if would exceed limit
            if (Math.Abs(newExposure) > Constants.ExposureLimitReais)
            {
                var rejectReason = $"Exposure limit exceeded. Current: {currentExposure:C}, " +
                                  $"Impact: {impact:C}, Would be: {newExposure:C}. " +
                                  $"Limit: ±{Constants.ExposureLimitReais:C}";

                return ProcessingResult.CreateRejected(rejectReason, currentExposure);
            }

            // Atomically update exposure state
            _exposures.AddOrUpdate(orderInfo.Symbol, newExposure, (_, __) => newExposure);

            // Generate unique order ID
            string orderId = GenerateOrderId();

            // Log successful processing
            Console.WriteLine($"[ORDER ACCEPTED] {orderInfo.Symbol} | " +
                            $"Exposure: {currentExposure:C} → {newExposure:C} | " +
                            $"OrderID: {orderId}");

            return ProcessingResult.CreateAccepted(orderId, newExposure);
        }

        /// <summary>
        /// Get current exposure for a symbol
        /// </summary>
        public decimal GetExposure(string symbol)
        {
            return _exposures.TryGetValue(symbol, out var exposure) ? exposure : 0m;
        }

        /// <summary>
        /// Get snapshot of all exposures (for logging/monitoring)
        /// </summary>
        public IReadOnlyDictionary<string, decimal> GetAllExposures()
        {
            return _exposures.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Print current state to console
        /// </summary>
        public void PrintState()
        {
            Console.WriteLine("\n=== EXPOSURE STATE ===");
            foreach (var symbol in Constants.ValidSymbols)
            {
                decimal exp = GetExposure(symbol);
                string status = Math.Abs(exp) > Constants.ExposureLimitReais * 0.8m ? "⚠️ HIGH" : "✓ OK";
                Console.WriteLine($"  {symbol}: {exp:C} {status}");
            }
            Console.WriteLine("======================\n");
        }

        /// <summary>
        /// Reset exposure for a symbol (useful for testing, not production)
        /// </summary>
        public void ResetExposure(string symbol)
        {
            _exposures.TryRemove(symbol, out _);
        }

        /// <summary>
        /// Generate unique order ID using thread-safe counter
        /// Format: ORD-20260620-000001, ORD-20260620-000002, etc
        /// </summary>
        private string GenerateOrderId()
        {
            lock (_orderIdLock)
            {
                _orderIdCounter++;
                string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
                return $"ORD-{dateStr}-{_orderIdCounter:D6}";
            }
        }
    }
}
