using System;
using System.Collections.Generic;
using System.Globalization;
using QuickFix;
using QuickFix.Store;
using OrderAccumulator.Core;
using Microsoft.Extensions.Logging;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// Reconstructs ExposureState from the local QuickFIX/n FileStore before the Acceptor
    /// starts accepting new connections.
    ///
    /// The FIX FileStore persists all messages SENT by this session (ExecutionReports) so that
    /// they can be replayed if a counterparty requests a ResendRequest. Each accepted
    /// ExecutionReport (ExecType='0') contains Symbol, Side, OrderQty (Tag 38) and Price (Tag 44) — the
    /// fields needed to compute the original exposure impact.
    ///
    /// This approach:
    ///   - Reads only local files, no network call needed
    ///   - Keeps the FIX FileStore as the single source of truth
    ///   - Must complete before the Acceptor starts so there is no concurrency on ExposureState
    /// </summary>
    public class FIXStoreExposureReconstructor
    {
        // FIX field tags
        private const int TAG_MSGTYPE = 35;
        private const int TAG_POSSDUPFLAG = 43;
        private const int TAG_EXECTYPE = 150;
        private const int TAG_SYMBOL = 55;
        private const int TAG_SIDE = 54;
        private const int TAG_ORDERQTY = 38;   // order quantity in NewOrderSingle
        private const int TAG_PRICE = 44;   // price in NewOrderSingle
        private const int TAG_ORDERID = 37;

        private readonly ExposureState _exposureState;
        private readonly SessionSettings _settings;
        private readonly ILogger<FIXStoreExposureReconstructor> _logger;

        public FIXStoreExposureReconstructor(
            ExposureState exposureState, 
            SessionSettings settings, 
            ILogger<FIXStoreExposureReconstructor> logger)
        {
            _exposureState  = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
            _settings       = settings      ?? throw new ArgumentNullException(nameof(settings));
            _logger         = logger        ?? throw new ArgumentNullException(nameof(logger));
        }        

        /// <summary>
        /// Reads all messages stored in the FileStore for every session configured in
        /// <paramref name="settings"/> and applies accepted-order exposure impacts to
        /// <paramref name="exposureState"/>.
        /// </summary>
        /// <returns>Number of historical orders applied.</returns>
        public int Reconstruct()
        {
            int totalApplied = 0;

            foreach (SessionID sessionID in _settings.GetSessions())
            {
                _logger.LogInformation($"[STORE-REPLAY] Reading FileStore for session {sessionID}...");
                totalApplied += ReconstructSession(sessionID);
            }

            return totalApplied;
        }

        private int ReconstructSession(SessionID sessionID)
        {
            var storeFactory = new FileStoreFactory(_settings);
            using IMessageStore store = storeFactory.Create(sessionID);
            ulong nextSender = store.NextSenderMsgSeqNum;

            if (nextSender <= 1)
            {
                _logger.LogInformation("[STORE-REPLAY] No stored messages — clean start.");                
                return 0;
            }            

            var rawMessages = new List<string>();
            store.Get(1, nextSender - 1, rawMessages);

            _logger.LogInformation("[STORE-REPLAY] Found {Count} stored messages (sender seqnums 1..{EndSeqNum}).",
                                  rawMessages.Count, nextSender - 1);

            int applied = 0;
            foreach (string rawMsg in rawMessages)
            {
                try
                {
                    applied += TryApplyMessage(_exposureState, rawMsg);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[STORE-REPLAY] ⚠️  Skipped unparseable message: {Message}", ex.Message);
                }
            }

            return applied;
        }

        private int TryApplyMessage(ExposureState exposureState, string rawMsg)
        {
            string? msgType = ExtractField(rawMsg, TAG_MSGTYPE);
            if (msgType == null) return 0;

            string? possDup = ExtractField(rawMsg, TAG_POSSDUPFLAG);
            if (possDup == "Y") return 0;

            return msgType switch
            {
                // ExecutionReport: message SENT by this Accumulator.
                "8" => TryApplyExecutionReport(exposureState, rawMsg),

                // Admin messages (Logon='A', Heartbeat='0', etc.) — ignore
                _ => 0
            };
        }

        private int TryApplyExecutionReport(ExposureState exposureState, string rawMsg)
        {
            // Only accepted/new ExecutionReports (ExecType='0') affect exposure.
            // Rejected do not change exposure.
            string? execType = ExtractField(rawMsg, TAG_EXECTYPE);
            if (execType != "0") return 0;

            string? symbol   = ExtractField(rawMsg, TAG_SYMBOL);
            string? sideStr  = ExtractField(rawMsg, TAG_SIDE);
            string? orderQty = ExtractField(rawMsg, TAG_ORDERQTY);
            string? priceStr = ExtractField(rawMsg, TAG_PRICE);
            string? orderId  = ExtractField(rawMsg, TAG_ORDERID);

            if (symbol == null || sideStr == null || orderQty == null || priceStr == null)
            {
                _logger.LogWarning("[STORE-REPLAY] ⚠️  ExecutionReport missing required fields — skipped.");
                return 0;
            }

            if (!IsValidSymbol(symbol))
            {
                _logger.LogWarning("[STORE-REPLAY] ⚠️  Unknown symbol '{Symbol}' — skipped.", symbol);
                return 0;
            }

            decimal qty    = decimal.Parse(orderQty, CultureInfo.InvariantCulture);
            decimal price  = decimal.Parse(priceStr, CultureInfo.InvariantCulture);
            decimal impact = sideStr == "1" ? qty * price : -(qty * price);   // Buy=+, Sell=-

            exposureState.ApplyHistoricalExposure(symbol, impact);

            _logger.LogInformation("[STORE-REPLAY] ✅  ExecutionReport {OrderId}: {Symbol} Side={Side} Qty={Qty} Px={Price} → impact={Impact}",
                                  orderId, symbol, sideStr, qty, price, impact);
            return 1;
        }        

        #region Helpers

        private static bool IsValidSymbol(string symbol) =>
            Array.Exists(Core.Constants.ValidSymbols, s => s == symbol);

        /// <summary>
        /// Extracts the value of a FIX field from a raw FIX message string.
        /// FIX fields are delimited by SOH (\u0001 / ASCII 1).
        /// </summary>
        private static string? ExtractField(string rawMsg, int tag)
        {
            // BeginString (tag 8) is always first and has no leading SOH
            string prefix = tag == 8 ? $"{tag}=" : $"\u0001{tag}=";

            int idx = rawMsg.IndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += prefix.Length;
            int endIdx = rawMsg.IndexOf('\u0001', idx);
            return endIdx < 0 ? rawMsg.Substring(idx) : rawMsg.Substring(idx, endIdx - idx);
        }

        #endregion
    }
}
