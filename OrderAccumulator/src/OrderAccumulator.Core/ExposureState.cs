using System;
using System.Collections.Generic;
using OrderAccumulator.Core.Models;

namespace OrderAccumulator.Core
{
    /// <summary>
    /// Tracks cumulative exposure per symbol and accepts/rejects orders against
    /// the ±R$ 100M limit. Live processing (TryProcessOrder, GetExposure) is
    /// lock-protected; replay methods run only at startup, before concurrency
    /// is possible, so they're unlocked by design.
    ///
    /// Out of scope: input validation, FIX protocol, persistence.
    /// </summary>
    public class ExposureState
    {
        private readonly Dictionary<string, decimal> _exposures = new();
        private readonly object _stateLock = new();
        private long _orderIdCounter = 0;

        public ExposureState()
        {
            foreach (var symbol in Constants.ValidSymbols)
                _exposures[symbol] = 0m;
        }

        /// <summary>
        /// Atomically checks the exposure limit and updates state. Every order
        /// (accepted or rejected) consumes an OrderID, keeping the counter
        /// consistent with replay (see AdvanceOrderIdCounter).
        /// </summary>
        public ProcessingResult TryProcessOrder(OrderInfo orderInfo)
        {
            decimal impact = orderInfo.CalculateExposureImpact();

            _orderIdCounter++;
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string orderId = $"ORD-{dateStr}-{_orderIdCounter:D6}";

            decimal currentExposure = _exposures.TryGetValue(orderInfo.Symbol, out var exp) ? exp : 0m;
            decimal newExposure = currentExposure + impact;

            if (Math.Abs(newExposure) > Constants.ExposureLimitReais)
            {
                var rejectReason = $"Exposure limit exceeded. Current: {currentExposure:C}, " +
                                  $"Impact: {impact:C}, Would be: {newExposure:C}. " +
                                  $"Limit: ±{Constants.ExposureLimitReais:C}";
                return ProcessingResult.CreateRejected(orderId, rejectReason, currentExposure);
            }

            _exposures[orderInfo.Symbol] = newExposure;
            return ProcessingResult.CreateAccepted(orderId, newExposure);
        }

        /// <summary>
        /// Replay-only: advances the OrderID counter for a historically rejected
        /// order, without touching exposure.
        /// </summary>
        public void AdvanceOrderIdCounter() => _orderIdCounter++;

        /// <summary>
        /// Replay-only: re-applies a historically accepted order's exposure
        /// impact and advances the OrderID counter, bypassing the limit check
        /// since this order was already accepted in a prior session.
        /// </summary>
        public void ApplyHistoricalExposure(string symbol, decimal impact)
        {
            decimal current = _exposures.TryGetValue(symbol, out var exp) ? exp : 0m;
            _exposures[symbol] = current + impact;
            _orderIdCounter++;
        }

        /// <summary>Current exposure for a symbol.</summary>
        public decimal GetExposure(string symbol)
        {
            lock (_stateLock)
                return _exposures.TryGetValue(symbol, out var exposure) ? exposure : 0m;
        }

        /// <summary>Prints current exposure for all symbols to console.</summary>
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
    }
}