using System;
using System.Collections.Generic;
using System.Globalization;
using QuickFix;
using QuickFix.Store;
using QuickFix.Fields;
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
                    applied += TryApplyMessage(rawMsg);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[STORE-REPLAY] ⚠️  Skipped unparseable message: {Message}", ex.Message);
                }
            }

            return applied;
        }

        private int TryApplyMessage(string rawMsg)
        {
            try
            {
                var msg = new QuickFix.Message(rawMsg);

                string msgType = msg.Header.GetString(Tags.MsgType);
                if (msgType != MsgType.EXECUTION_REPORT) return 0;

                if (msg.IsSetField(Tags.PossDupFlag))
                {
                    string possDup = msg.GetString(Tags.PossDupFlag);
                    if (possDup == "Y") return 0;
                }

                return TryApplyExecutionReport(msg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[STORE-REPLAY] ⚠️ Failed to parse raw message: {Message}", ex.Message);
                return 0;
            }
        }

        private int TryApplyExecutionReport(QuickFix.Message msg)
        {
            if (!msg.IsSetField(Tags.ExecType))
                return 0;

            char execType = msg.GetChar(Tags.ExecType);            
            if (execType == ExecType.REJECTED)
            {
                _exposureState.AdvanceOrderIdCounter();
                return 1;
            }

            if (execType != ExecType.NEW)
                return 0;

            if (!msg.IsSetField(Tags.Symbol) || !msg.IsSetField(Tags.Side) || 
                !msg.IsSetField(Tags.OrderQty) || !msg.IsSetField(Tags.Price))
            {
                _logger.LogWarning("[STORE-REPLAY] ⚠️  ExecutionReport missing required fields — skipped.");
                return 0;
            }

            string symbol = msg.GetString(Tags.Symbol);
            string sideStr = msg.GetString(Tags.Side);
            decimal qty    = msg.GetDecimal(Tags.OrderQty);
            decimal price  = msg.GetDecimal(Tags.Price);
            decimal impact = sideStr == "1" ? qty * price : -(qty * price);

            _exposureState.ApplyHistoricalExposure(symbol, impact);            

            string orderId = msg.IsSetField(Tags.OrderID) ? msg.GetString(Tags.OrderID) : "unknown";
            _logger.LogInformation("[STORE-REPLAY] ✅  ExecutionReport {OrderId}: {Symbol} Side={Side} Qty={Qty} Px={Price} → impact={Impact}",
                                  orderId, symbol, sideStr, qty, price, impact);
            return 1;
        }
    }
}
