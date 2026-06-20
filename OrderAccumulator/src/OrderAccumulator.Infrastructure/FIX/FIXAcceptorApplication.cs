using System;
using System.Collections.Generic;
using QuickFix;
using QuickFix.Fields;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using static OrderAccumulator.Core.Constants;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// FIX Acceptor Application - Handles incoming FIX messages (NewOrderSingle)
    /// and sends ExecutionReport responses.
    /// </summary>
    public class FIXAcceptorApplication : IApplication
    {
        private readonly ExposureState _exposureState;
        private readonly FIXMessageConverter _converter;
        private static int _execIDCounter = 0;

        public FIXAcceptorApplication(ExposureState exposureState)
        {
            _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
            _converter = new FIXMessageConverter();
        }

        /// <summary>
        /// IApplication.OnCreate - Called when a new session is created.
        /// </summary>
        public void OnCreate(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] Session created: {sessionID}");
            ReconstructStateFromStore(sessionID);
        }

        /// <summary>
        /// IApplication.OnLogon - Called when logon is completed.
        /// </summary>
        public void OnLogon(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] ✅ Logon completed: {sessionID}");
        }

        /// <summary>
        /// IApplication.OnLogout - Called when logout is completed.
        /// </summary>
        public void OnLogout(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] ❌ Logout completed: {sessionID}");
        }

        /// <summary>
        /// IApplication.FromAdmin - Called when admin message received (logon, logout, heartbeat, etc).
        /// </summary>
        public void FromAdmin(QuickFix.Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            string seqNum = msg.Header.GetString(Tags.MsgSeqNum);
            Console.WriteLine($"[FIX-ADMIN] 📬 Received admin message:");
            Console.WriteLine($"  MsgType={msgType} | SeqNum={seqNum}");
            Console.WriteLine($"  Full: {msg}");
            
            // Debug: se receber MsgType=D, alertar ALTO
            if (msgType.Equals("D"))
            {
                Console.WriteLine($"[FIX-ADMIN] 🚨🚨🚨 CRITICAL: NewOrderSingle (MsgType=D) received in FromAdmin instead of FromApp!");
            }
        }

        /// <summary>
        /// IApplication.FromApp - Called when application message received.
        /// Routes NewOrderSingle messages to handler.
        /// </summary>
        public void FromApp(QuickFix.Message msg, SessionID sessionID)
        {
            try
            {
                string msgType = msg.Header.GetString(Tags.MsgType);
                string seqNum = msg.Header.GetString(Tags.MsgSeqNum);
                Console.WriteLine($"[FIX-APP] 🔍🔍🔍 FromApp CALLED!");
                Console.WriteLine($"  MsgType={msgType} | SeqNum={seqNum}");
                Console.WriteLine($"  Full: {msg}");
                
                if (msgType.Equals("D"))  // NewOrderSingle
                {
                    Console.WriteLine($"[FIX] 📨 NewOrderSingle received");

                    // Parse incoming order using generic message API
                    var orderInfo = _converter.ParseNewOrderSingle(msg);
                    Console.WriteLine($"[FIX] Parsed: {orderInfo.Symbol} {orderInfo.Side} x{orderInfo.Quantity} @ {orderInfo.Price} (ClOrdID={orderInfo.ClOrdID})");

                    // Process against ExposureState
                    var result = _exposureState.TryProcessOrder(orderInfo);

                    // Generate execution report ID
                    string execID = GenerateExecID();

                    // Build ExecutionReport response using generic message API
                    var execReport = _converter.BuildExecutionReport(msg, result, execID);

                    // Send response
                    Session.SendToTarget(execReport, sessionID);

                    if (result.Accepted)
                    {
                        Console.WriteLine($"[FIX] ✅ Order ACCEPTED - OrderID={result.OrderID}, Exposure={result.ResultingExposure:C}");
                    }
                    else
                    {
                        Console.WriteLine($"[FIX] ❌ Order REJECTED - {result.RejectReason}");
                    }
                }
                else
                {
                    Console.WriteLine($"[FIX] ⚠️ Unhandled MsgType in FromApp: {msgType}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIX-ERROR] Exception in FromApp: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// IApplication.ToApp - Called when application message is being sent.
        /// </summary>
        public void ToApp(QuickFix.Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            Console.WriteLine($"[FIX-TOAPP] 📤 Outbound app message: MsgType={msgType}");
        }

        /// <summary>
        /// IApplication.ToAdmin - Called when admin message is being sent.
        /// </summary>
        public void ToAdmin(QuickFix.Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            Console.WriteLine($"[FIX-TOADMIN] 📤 Outbound admin message: MsgType={msgType}");
        }

        private string GenerateExecID()
        {
            lock (typeof(FIXAcceptorApplication))
            {
                _execIDCounter++;
                return $"EXEC-{DateTime.UtcNow:yyyyMMdd}-{_execIDCounter:D6}";
            }
        }

        /// <summary>
        /// Reconstructs the ephemeral in-memory ExposureState by reading sent ExecutionReports from the FileStore.
        /// This ensures crash-recovery and sequence-number continuity.
        /// </summary>
        private void ReconstructStateFromStore(SessionID sessionID)
        {
            try
            {
                var session = Session.LookupSession(sessionID);
                if (session == null)
                {
                    Console.WriteLine("[FIX-REPLAY] Warning: session not found for ID: " + sessionID);
                    return;
                }

                ulong nextSenderSeqNum = session.NextSenderMsgSeqNum;
                ulong totalSent = nextSenderSeqNum - 1;
                Console.WriteLine($"[FIX-REPLAY] Starting state reconstruction. Next expected sender seqnum: {nextSenderSeqNum}. Messages to read: {totalSent}");

                if (totalSent == 0)
                {
                    Console.WriteLine("[FIX-REPLAY] No messages in store to replay.");
                    return;
                }

                var rawMessages = new List<string>();
                session.MessageStore.Get(1UL, totalSent, rawMessages);

                Console.WriteLine($"[FIX-REPLAY] Retrieved {rawMessages.Count} messages from store.");

                foreach (var rawMsg in rawMessages)
                {
                    try
                    {
                        var message = new QuickFix.Message(rawMsg, false);
                        string msgType = message.Header.GetString(Tags.MsgType);
                        
                        // We only care about ExecutionReports (MsgType='8')
                        if (msgType == "8")
                        {
                            string ordStatus = message.GetString(39); // OrdStatus
                            
                            // Only count accepted orders (FILLED or NEW)
                            if (ordStatus == "2" || ordStatus == "0")
                            {
                                string symbol = message.GetString(55); // Symbol
                                char sideChar = message.GetString(54)[0]; // Side
                                decimal quantity = message.GetDecimal(14); // CumQty (original quantity)
                                decimal price = message.GetDecimal(6); // AvgPx (original price)
                                string clOrdID = message.IsSetField(11) ? message.GetString(11) : "REPLAY";

                                // Reconstruct OrderInfo
                                var side = sideChar switch
                                {
                                    '1' => OrderSide.Buy,
                                    '2' => OrderSide.Sell,
                                    _ => throw new ArgumentException($"Invalid side: {sideChar}")
                                };

                                var orderInfo = new OrderInfo
                                {
                                    Symbol = symbol,
                                    Side = side,
                                    Quantity = quantity,
                                    Price = price,
                                    ClOrdID = clOrdID
                                };

                                // Replay into ExposureState
                                _exposureState.TryProcessOrder(orderInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FIX-REPLAY-ERROR] Failed to parse/replay message: {ex.Message}");
                    }
                }

                Console.WriteLine($"[FIX-REPLAY] Replay complete. Current exposure state:");
                _exposureState.PrintState();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIX-REPLAY-ERROR] Critical failure in ReconstructStateFromStore: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
