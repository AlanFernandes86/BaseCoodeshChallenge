using Microsoft.Extensions.Logging;
using OrderAccumulator.Core;
using QuickFix;
using QuickFix.Fields;
using System;
using System.Threading;

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
        private readonly ILogger<FIXAcceptorApplication> _logger;
        private readonly ExposureState _exposureState;
        private static int _execIDCounter = 0;

        public FIXAcceptorApplication(ExposureState exposureState, ILogger<FIXAcceptorApplication> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
        }

        public void OnCreate(SessionID sessionID)
        {
            _logger.LogInformation($"[FIX] Session created: {sessionID}");
        }

        public void OnLogon(SessionID sessionID)
        {
            _logger.LogInformation($"[FIX] ✅ Logon completed: {sessionID}");
        }

        public void OnLogout(SessionID sessionID)
        {
            _logger.LogInformation($"[FIX] ❌ Logout: {sessionID}");
        }

        public void FromAdmin(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            string seqNum  = msg.Header.GetString(Tags.MsgSeqNum);
            _logger.LogInformation($"[FIX-ADMIN] 📬 Received admin message: MsgType={msgType} | SeqNum={seqNum}");
        }

        /// <summary>
        /// IApplication.FromApp — called when an application message is received.
        /// Routes NewOrderSingle messages to ExposureState and sends ExecutionReport back.
        ///
        /// PossDupFlag=Y messages (replays from backend resend) are silently ignored:
        /// - Their exposure was already reconstructed from the FileStore at startup
        /// - Re-applying them would double-count exposure
        /// </summary>
        public void FromApp(Message msg, SessionID sessionID)
        {
            try
            {
                string msgType = msg.Header.GetString(Tags.MsgType);
                string seqNum  = msg.Header.GetString(Tags.MsgSeqNum);

                if (!msgType.Equals("D"))
                {
                    _logger.LogWarning($"[FIX] ⚠️ Unhandled MsgType in FromApp: {msgType}");
                    return;
                }

                // Live order — parse, evaluate, respond
                var orderInfo = msg.ToOrderInfo();
                var result  = _exposureState.TryProcessOrder(orderInfo);
                string execID = GenerateExecID();
                var execReport = msg.ToExecutionReport(result, execID);
                Session.SendToTarget(execReport, sessionID);

                if (result.Accepted)
                    _logger.LogInformation($"[FIX] ✅ ACCEPTED OrderID={result.OrderID}, Exposure={result.ResultingExposure:C}");
                else
                    _logger.LogWarning($"[FIX] ❌ REJECTED {result.RejectReason}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FIX-ERROR] Exception in FromApp: {Message}", ex.Message);
            }
        }

        public void ToApp(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            _logger.LogInformation($"[FIX-TOAPP] 📤 Outbound app message: MsgType={msgType}");
        }

        public void ToAdmin(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            _logger.LogInformation($"[FIX-TOADMIN] 📤 Outbound admin message: MsgType={msgType}");
        }

        private string GenerateExecID() =>
            $"EXEC-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _execIDCounter):D6}";
    }
}
