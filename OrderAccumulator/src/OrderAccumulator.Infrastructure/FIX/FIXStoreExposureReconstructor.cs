using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuickFix;
using QuickFix.Store;
using OrderAccumulator.Core;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// Reconstructs ExposureState from the local QuickFIX/n FileStore before the Acceptor
    /// starts accepting new connections.
    ///
    /// The FIX FileStore persists all messages SENT by this session (ExecutionReports) so that
    /// they can be replayed if a counterparty requests a ResendRequest. Each accepted
    /// ExecutionReport (OrdStatus='2') contains Symbol, Side, CumQty and AvgPx — the same
    /// fields needed to compute the original exposure impact.
    ///
    /// This approach:
    ///   - Reads only local files, no network call needed
    ///   - Is decoupled from the FIX session protocol (no ResendRequest over the wire)
    ///   - Keeps the FIX FileStore as the single source of truth
    ///   - Must complete before the Acceptor starts so there is no concurrency on ExposureState
    /// </summary>
    public class FIXStoreExposureReconstructor
    {
        // FIX field tags
        private const int TAG_MSGTYPE     = 35;
        private const int TAG_POSSDUPFLAG = 43;
        private const int TAG_ORDSTATUS   = 39;
        private const int TAG_SYMBOL      = 55;
        private const int TAG_SIDE        = 54;
        private const int TAG_CUMQTY      = 14;   // filled quantity in ExecutionReport
        private const int TAG_AVGPX       = 6;    // average price in ExecutionReport
        private const int TAG_ORDERQTY    = 38;   // order quantity in NewOrderSingle
        private const int TAG_PRICE       = 44;   // price in NewOrderSingle
        private const int TAG_ORDERID     = 37;
        private const int TAG_CLORDID     = 11;

        /// <summary>
        /// Reads all messages stored in the FileStore for every session configured in
        /// <paramref name="settings"/> and applies accepted-order exposure impacts to
        /// <paramref name="exposureState"/>.
        /// </summary>
        /// <returns>Number of historical orders applied.</returns>
        public int Reconstruct(ExposureState exposureState, SessionSettings settings)
        {
            int totalApplied = 0;

            foreach (SessionID sessionID in settings.GetSessions())
            {
                Console.WriteLine($"\n[STORE-REPLAY] 🗄️  Reading FileStore for session {sessionID}...");
                totalApplied += ReconstructSession(exposureState, settings, sessionID);
            }

            return totalApplied;
        }

        private int ReconstructSession(ExposureState exposureState, SessionSettings settings, SessionID sessionID)
        {
            // Determine the FileStorePath configured for this session
            string fileStorePath = GetFileStorePath(settings, sessionID);

            // Read the next-sender sequence number from the .seqnums file directly.
            // QuickFIX/n FileStore writes: "{nextSenderNum}:{nextTargetNum}" in this file.
            // We avoid IMessageStore.GetNextSenderMsgSeqNum() because its signature varies
            // across QuickFIX/n versions.
            ulong nextSender = ReadNextSenderSeqNum(fileStorePath, sessionID);

            if (nextSender <= 1)
            {
                Console.WriteLine("[STORE-REPLAY] No stored messages — clean start.");
                return 0;
            }

            // Open the FileStore to read stored messages. Dispose after reading to
            // release file handles before the Acceptor opens its own store instance.
            var storeFactory = new FileStoreFactory(settings);
            using IMessageStore store = storeFactory.Create(sessionID);

            var rawMessages = new List<string>();
            // endSeqNo must be ulong in QuickFIX/n 1.14.x; beginSeqNo is int.
            // Retrieve all stored messages from seqnum 1 to (nextSender - 1).
            store.Get(1, nextSender - 1, rawMessages);

            Console.WriteLine($"[STORE-REPLAY] Found {rawMessages.Count} stored messages " +
                              $"(sender seqnums 1..{nextSender - 1}).");

            int applied = 0;
            foreach (string rawMsg in rawMessages)
            {
                try
                {
                    applied += TryApplyMessage(exposureState, rawMsg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[STORE-REPLAY] ⚠️  Skipped unparseable message: {ex.Message}");
                }
            }

            return applied;
        }

        // ── Sequence number helpers ───────────────────────────────────────────────

        /// <summary>
        /// Gets the FileStorePath for a session from SessionSettings.
        /// Falls back to the DEFAULT section if the session-specific entry is not found.
        /// </summary>
        private static string GetFileStorePath(SessionSettings settings, SessionID sessionID)
        {
            try
            {
                return settings.Get(sessionID).GetString("FileStorePath");
            }
            catch
            {
                try { return settings.Get().GetString("FileStorePath"); }
                catch { return "./store"; }
            }
        }

        /// <summary>
        /// Reads the next sender sequence number from the QuickFIX/n .seqnums file.
        /// File location: {FileStorePath}/{BeginString}-{SenderCompID}-{TargetCompID}.seqnums
        /// File format:   "nextSenderNum:nextTargetNum" (plain text, no header)
        /// Returns 1 if the file does not exist (no messages sent yet).
        /// </summary>
        private static ulong ReadNextSenderSeqNum(string storePath, SessionID sessionID)
        {
            string fileName = $"{sessionID.BeginString}-{sessionID.SenderCompID}-{sessionID.TargetCompID}.seqnums";
            string filePath = Path.Combine(storePath, fileName);

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[STORE-REPLAY] Seqnums file not found: {filePath}. Assuming clean start.");
                return 1;
            }

            string content = File.ReadAllText(filePath).Trim();
            Console.WriteLine($"[STORE-REPLAY] Seqnums file: '{content}' ({filePath})");

            string[] parts = content.Split(':');
            if (parts.Length < 1 || !ulong.TryParse(parts[0], out ulong nextSender))
            {
                Console.WriteLine("[STORE-REPLAY] ⚠️  Could not parse seqnums file. Assuming clean start.");
                return 1;
            }

            return nextSender;
        }

        // ── Message processing ────────────────────────────────────────────────────

        private int TryApplyMessage(ExposureState exposureState, string rawMsg)
        {
            string? msgType = ExtractField(rawMsg, TAG_MSGTYPE);
            if (msgType == null) return 0;

            // Skip resent copies — they are duplicates of already-applied messages
            string? possDup = ExtractField(rawMsg, TAG_POSSDUPFLAG);
            if (possDup == "Y") return 0;

            return msgType switch
            {
                // ExecutionReport: message SENT by this Accumulator.
                // Only accepted orders (OrdStatus='2') change exposure.
                "8" => TryApplyExecutionReport(exposureState, rawMsg),

                // NewOrderSingle: message RECEIVED by this Accumulator.
                // QuickFIX/n does not normally store received messages in the FileStore,
                // but handled here for completeness / forward compatibility.
                "D" => TryApplyNewOrderSingle(exposureState, rawMsg),

                // Admin messages (Logon='A', Heartbeat='0', etc.) — ignore
                _ => 0
            };
        }

        private int TryApplyExecutionReport(ExposureState exposureState, string rawMsg)
        {
            // Only FILLED / accepted orders affect exposure (OrdStatus='2')
            string? ordStatus = ExtractField(rawMsg, TAG_ORDSTATUS);
            if (ordStatus != "2") return 0;

            string? symbol  = ExtractField(rawMsg, TAG_SYMBOL);
            string? sideStr = ExtractField(rawMsg, TAG_SIDE);
            string? cumQty  = ExtractField(rawMsg, TAG_CUMQTY);
            string? avgPx   = ExtractField(rawMsg, TAG_AVGPX);
            string? orderId = ExtractField(rawMsg, TAG_ORDERID);

            if (symbol == null || sideStr == null || cumQty == null || avgPx == null)
            {
                Console.WriteLine("[STORE-REPLAY] ⚠️  ExecutionReport missing required fields — skipped.");
                return 0;
            }

            if (!IsValidSymbol(symbol))
            {
                Console.WriteLine($"[STORE-REPLAY] ⚠️  Unknown symbol '{symbol}' — skipped.");
                return 0;
            }

            decimal qty    = decimal.Parse(cumQty, CultureInfo.InvariantCulture);
            decimal price  = decimal.Parse(avgPx,  CultureInfo.InvariantCulture);
            decimal impact = sideStr == "1" ? qty * price : -(qty * price);   // Buy=+, Sell=-

            exposureState.ApplyHistoricalExposure(symbol, impact);

            Console.WriteLine($"[STORE-REPLAY] ✅  ExecutionReport {orderId}: " +
                              $"{symbol} Side={sideStr} Qty={qty} Px={price} → impact={impact:C}");
            return 1;
        }

        private int TryApplyNewOrderSingle(ExposureState exposureState, string rawMsg)
        {
            string? symbol   = ExtractField(rawMsg, TAG_SYMBOL);
            string? sideStr  = ExtractField(rawMsg, TAG_SIDE);
            string? qtyStr   = ExtractField(rawMsg, TAG_ORDERQTY);
            string? priceStr = ExtractField(rawMsg, TAG_PRICE);
            string? clOrdId  = ExtractField(rawMsg, TAG_CLORDID);

            if (symbol == null || sideStr == null || qtyStr == null || priceStr == null)
            {
                Console.WriteLine("[STORE-REPLAY] ⚠️  NewOrderSingle missing required fields — skipped.");
                return 0;
            }

            if (!IsValidSymbol(symbol))
            {
                Console.WriteLine($"[STORE-REPLAY] ⚠️  Unknown symbol '{symbol}' — skipped.");
                return 0;
            }

            decimal qty    = decimal.Parse(qtyStr,   CultureInfo.InvariantCulture);
            decimal price  = decimal.Parse(priceStr, CultureInfo.InvariantCulture);
            decimal impact = sideStr == "1" ? qty * price : -(qty * price);

            exposureState.ApplyHistoricalExposure(symbol, impact);

            Console.WriteLine($"[STORE-REPLAY] ✅  NewOrderSingle {clOrdId}: " +
                              $"{symbol} Side={sideStr} Qty={qty} Px={price} → impact={impact:C}");
            return 1;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

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
    }
}
