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
        private bool _stateReconstructed = false;
        private ulong _endReplaySeqNum = 0;

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
        }

        /// <summary>
        /// IApplication.OnLogon - Called when logon is completed.
        /// </summary>
        public void OnLogon(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] ✅ Logon completed: {sessionID}");

            if (!_stateReconstructed)
            {
                try
                {
                    Session session = Session.LookupSession(sessionID);
                    if (session != null)
                    {
                        ulong nextTarget = session.NextTargetMsgSeqNum;
                        if (nextTarget > 1)
                        {
                            _endReplaySeqNum = nextTarget - 1;
                            Console.WriteLine($"[FIX-REPLAY] 🔄 Session expects target seqnum {nextTarget}. Triggering ResendRequest from 1 to {_endReplaySeqNum} to reconstruct exposure state...");
                            var resendRequest = new QuickFix.FIX44.ResendRequest(
                                new BeginSeqNo(1),
                                new EndSeqNo(_endReplaySeqNum)
                            );
                            Session.SendToTarget(resendRequest, sessionID);
                        }
                        else
                        {
                            Console.WriteLine("[FIX-REPLAY] No history to replay. Exposure state is clean.");
                            _stateReconstructed = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FIX-REPLAY-ERROR] Failed to trigger ResendRequest: {ex.Message}");
                }
            }
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
            
            if (!_stateReconstructed && _endReplaySeqNum > 0)
            {
                if (ulong.TryParse(seqNum, out ulong msgSeqNum) && msgSeqNum >= _endReplaySeqNum)
                {
                    Console.WriteLine($"[FIX-REPLAY] ✅ Replay complete (detected admin msg sequence {msgSeqNum} >= {_endReplaySeqNum}). Exposure state fully reconstructed!");
                    _stateReconstructed = true;
                }
            }

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

                    // Check if it's a replayed message (PossDupFlag=Y)
                    bool isPossDup = msg.Header.IsSetField(Tags.PossDupFlag) && msg.Header.GetString(Tags.PossDupFlag) == "Y";

                    if (isPossDup)
                    {
                        Console.WriteLine($"[FIX-REPLAY] 🔄 Replayed order {orderInfo.ClOrdID} processed. No response sent. Rebuilt Exposure={result.ResultingExposure:C}");
                        
                        if (!_stateReconstructed && _endReplaySeqNum > 0)
                        {
                            if (ulong.TryParse(seqNum, out ulong msgSeqNum) && msgSeqNum >= _endReplaySeqNum)
                            {
                                Console.WriteLine($"[FIX-REPLAY] ✅ Replay complete (detected app msg sequence {msgSeqNum} >= {_endReplaySeqNum}). Exposure state fully reconstructed!");
                                _stateReconstructed = true;
                            }
                        }
                    }
                    else
                    {
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
    }
}
