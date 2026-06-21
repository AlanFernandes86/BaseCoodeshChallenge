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
    ///
    /// State reconstruction happens BEFORE this application starts accepting connections —
    /// see FIXAcceptorHostedService.StartAsync. By the time FromApp is called,
    /// ExposureState is already correct. No ResendRequest or replay logic needed here.
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

        public void OnCreate(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] Session created: {sessionID}");
        }

        public void OnLogon(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] ✅ Logon completed: {sessionID}");
            // State was already reconstructed from FileStore before the Acceptor started.
            // Nothing to do here.
        }

        public void OnLogout(SessionID sessionID)
        {
            Console.WriteLine($"[FIX] ❌ Logout: {sessionID}");
        }

        public void FromAdmin(QuickFix.Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            string seqNum  = msg.Header.GetString(Tags.MsgSeqNum);
            Console.WriteLine($"[FIX-ADMIN] 📬 Received admin message: MsgType={msgType} | SeqNum={seqNum}");
        }

        /// <summary>
        /// IApplication.FromApp — called when an application message is received.
        /// Routes NewOrderSingle messages to ExposureState and sends ExecutionReport back.
        ///
        /// PossDupFlag=Y messages (replays from backend resend) are silently ignored:
        /// - Their exposure was already reconstructed from the FileStore at startup
        /// - Re-applying them would double-count exposure
        /// </summary>
        public void FromApp(QuickFix.Message msg, SessionID sessionID)
        {
            try
            {
                string msgType = msg.Header.GetString(Tags.MsgType);
                string seqNum  = msg.Header.GetString(Tags.MsgSeqNum);

                if (!msgType.Equals("D"))
                {
                    Console.WriteLine($"[FIX] ⚠️ Unhandled MsgType in FromApp: {msgType}");
                    return;
                }

                // Detect replayed messages (PossDupFlag=Y from backend during session recovery)
                bool isPossDup = msg.Header.IsSetField(Tags.PossDupFlag)
                                 && msg.Header.GetString(Tags.PossDupFlag) == "Y";

                if (isPossDup)
                {
                    // State was already reconstructed from FileStore — do NOT re-apply.
                    string clOrdId = msg.IsSetField(11) ? msg.GetString(11) : "?";
                    Console.WriteLine($"[FIX] ♻️  Ignored replayed NewOrderSingle [{seqNum}] ClOrdID={clOrdId} (PossDupFlag=Y)");
                    return;
                }

                // Live order — parse, evaluate, respond
                var orderInfo = _converter.ParseNewOrderSingle(msg);
                Console.WriteLine($"[FIX] 📨 NewOrderSingle [{seqNum}]: " +
                                  $"{orderInfo.Symbol} {orderInfo.Side} x{orderInfo.Quantity} @ {orderInfo.Price} " +
                                  $"(ClOrdID={orderInfo.ClOrdID})");

                var result  = _exposureState.TryProcessOrder(orderInfo);
                string execID = GenerateExecID();
                var execReport = _converter.BuildExecutionReport(msg, result, execID);
                Session.SendToTarget(execReport, sessionID);

                if (result.Accepted)
                    Console.WriteLine($"[FIX] ✅ ACCEPTED OrderID={result.OrderID}, Exposure={result.ResultingExposure:C}");
                else
                    Console.WriteLine($"[FIX] ❌ REJECTED {result.RejectReason}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIX-ERROR] Exception in FromApp: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void ToApp(QuickFix.Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            Console.WriteLine($"[FIX-TOAPP] 📤 Outbound app message: MsgType={msgType}");
        }

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
