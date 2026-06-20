using System;
using System.Collections.Generic;
using System.Threading;
using OrderAccumulator.Core.Models;

namespace OrderAccumulator.Core
{
    /// <summary>
    /// Pure domain logic for exposure tracking and order processing.
    /// Thread-safe via explicit lock for atomic check-and-update.
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
        // Use a plain Dictionary + lock so check+update is a single atomic operation.
        // A ConcurrentDictionary would still have a TOCTOU race between reading and writing.
        private readonly Dictionary<string, decimal> _exposures = new();
        private readonly object _stateLock = new();
        private long _orderIdCounter = 0;

        public ExposureState()
        {
            // Initialize all valid symbols to zero so PrintState always shows them
            foreach (var symbol in Constants.ValidSymbols)
                _exposures[symbol] = 0m;
        }

        /// <summary>
        /// Process an order: atomically check if new exposure would exceed limit, then update state.
        /// The entire check+update is inside a lock to prevent TOCTOU races under concurrency.
        /// </summary>
        public ProcessingResult TryProcessOrder(OrderInfo orderInfo)
        {
            decimal impact = orderInfo.CalculateExposureImpact();

            lock (_stateLock)
            {
                decimal currentExposure = _exposures.TryGetValue(orderInfo.Symbol, out var exp) ? exp : 0m;
                decimal newExposure = currentExposure + impact;

                // Check if would exceed limit
                if (Math.Abs(newExposure) > Constants.ExposureLimitReais)
                {
                    var rejectReason = $"Exposure limit exceeded. Current: {currentExposure:C}, " +
                                      $"Impact: {impact:C}, Would be: {newExposure:C}. " +
                                      $"Limit: ±{Constants.ExposureLimitReais:C}";

                    Console.WriteLine($"[ORDER REJECTED] {orderInfo.Symbol} | {rejectReason}");
                    return ProcessingResult.CreateRejected(rejectReason, currentExposure);
                }

                // Commit the new exposure
                _exposures[orderInfo.Symbol] = newExposure;

                // Generate unique order ID (inside lock so counter stays consistent)
                _orderIdCounter++;
                string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
                string orderId = $"ORD-{dateStr}-{_orderIdCounter:D6}";

                Console.WriteLine($"[ORDER ACCEPTED] {orderInfo.Symbol} | " +
                                $"Exposure: {currentExposure:C} → {newExposure:C} | " +
                                $"OrderID: {orderId}");

                return ProcessingResult.CreateAccepted(orderId, newExposure);
            }
        }

        /// <summary>
        /// Get current exposure for a symbol
        /// </summary>
        public decimal GetExposure(string symbol)
        {
            lock (_stateLock)
                return _exposures.TryGetValue(symbol, out var exposure) ? exposure : 0m;
        }

        /// <summary>
        /// Get snapshot of all exposures (for logging/monitoring)
        /// </summary>
        public IReadOnlyDictionary<string, decimal> GetAllExposures()
        {
            lock (_stateLock)
                return new Dictionary<string, decimal>(_exposures);
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
        /// Reset exposure for a symbol (useful for testing)
        /// </summary>
        public void ResetExposure(string symbol)
        {
            lock (_stateLock)
                _exposures[symbol] = 0m;
        }
    }
}
