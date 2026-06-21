using System;
using System.Collections.Concurrent;
using QuickFix;
using QuickFix.Store;
using QuickFix.Transport;
using QuickFix.Fields;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Domain.Services;
using Microsoft.Extensions.Logging;

namespace OrderGenerator.Infrastructure.FIX
{
    /// <summary>
    /// Manages FIX Initiator connection to the OrderAccumulator
    /// Sends NewOrderSingle messages and receives ExecutionReports
    /// </summary>
    public class FIXInitiatorService : IApplication, IFIXOrderSubmissionService
    {
        private SessionID? _sessionID;
        private SocketInitiator? _initiator;
        private readonly object _lock = new object();
        private readonly ILogger<FIXInitiatorService> _logger;
        
        // Queue for responses from Accumulator
        private readonly ConcurrentQueue<ExecutionReportData> _executionReports = new();

        public event EventHandler<ExecutionReportData>? ExecutionReportReceived;

        public bool IsSessionActive
        {
            get
            {
                lock (_lock)
                {
                    return _sessionID != null;
                }
            }
        }

        public FIXInitiatorService(ILogger<FIXInitiatorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Start FIX Initiator connection to Accumulator
        /// </summary>
        public void Start(string configFilePath)
        {
            lock (_lock)
            {
                try
                {
                    var settings = new SessionSettings(configFilePath);
                    var storeFactory = new FileStoreFactory(settings);
                    _initiator = new SocketInitiator(
                        this,
                        storeFactory,
                        settings,
                        (QuickFix.Logger.ILogFactory)null
                    );
                    _initiator.Start();
                    _logger.LogInformation("[FIX-INITIATOR] Started successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[FIX-INITIATOR-ERROR] Failed to start: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Stop FIX Initiator connection
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_initiator != null)
                {
                    _initiator.Stop();
                    _logger.LogInformation("[FIX-INITIATOR] Stopped");
                }
            }
        }

        /// <summary>
        /// Send NewOrderSingle to Accumulator (IFIXOrderSubmissionService implementation)
        /// </summary>
        public void SendNewOrderSingle(string symbol, OrderSide side, decimal quantity, decimal price, string clOrdID)
        {
            if (_sessionID == null)
            {
                throw new InvalidOperationException("FIX session not established");
            }

            try
            {
                _logger.LogInformation($"[FIX-INITIATOR] 📝 Building NewOrderSingle:");
                _logger.LogInformation($"[FIX-INITIATOR]   Symbol={symbol}, Side={side}, Qty={quantity}, Price={price}");
                _logger.LogInformation($"[FIX-INITIATOR]   ClOrdID={clOrdID}");
                _logger.LogInformation($"[FIX-INITIATOR]   SessionID={_sessionID}");

                // Create NewOrderSingle using Message API (MsgType='D')
                var order = new Message();
                
                // Set MsgType in header FIRST
                order.Header.SetField(new StringField(35, "D"));  // MsgType = NewOrderSingle
                
                // Verify MsgType was set
                string verifyMsgType = order.Header.GetString(Tags.MsgType);
                _logger.LogInformation($"[FIX-INITIATOR] 📋 After header set - MsgType={verifyMsgType}");
                
                // Set required fields
                order.SetField(new StringField(11, clOrdID));      // ClOrdID
                order.SetField(new StringField(55, symbol));       // Symbol
                order.SetField(new StringField(54, ((int)side).ToString()));  // Side: 1=Buy, 2=Sell
                order.SetField(new DecimalField(38, quantity));    // OrderQty
                order.SetField(new DecimalField(44, price));       // Price
                order.SetField(new StringField(40, "2"));          // OrdType = 2 (Limit)
                order.SetField(new StringField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));  // TransactTime (field 60)

                // Verify complete message before sending
                _logger.LogInformation($"[FIX-INITIATOR] ✅ Message ready:");
                _logger.LogInformation($"  MsgType={order.Header.GetString(Tags.MsgType)}");
                _logger.LogInformation($"  ClOrdID={order.GetString(11)}");
                _logger.LogInformation($"  Symbol={order.GetString(55)}");
                _logger.LogInformation($"  Side={order.GetString(54)}");
                _logger.LogInformation($"  Full Message:\n{order}");

                // Send to Accumulator
                bool sendResult = Session.SendToTarget(order, _sessionID);
                _logger.LogInformation($"[FIX-INITIATOR] ✅ SendToTarget result: {sendResult}");
                if (!sendResult)
                {
                    _logger.LogWarning($"[FIX-INITIATOR] ⚠️ SendToTarget returned FALSE!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FIX-INITIATOR-ERROR] Failed to send NewOrderSingle: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Try to get an execution report (non-blocking) (IFIXOrderSubmissionService implementation)
        /// </summary>
        public bool TryGetExecutionReport(out ExecutionReportData? report)
        {
            return _executionReports.TryDequeue(out report);
        }

        // ========== IApplication Implementation ==========

        public void OnCreate(SessionID sessionID)
        {
            _logger.LogInformation($"[FIX-INITIATOR] Session created: {sessionID}");
        }

        public void OnLogon(SessionID sessionID)
        {
            lock (_lock)
            {
                _sessionID = sessionID;
            }
            _logger.LogInformation($"[FIX-INITIATOR] Logon: {sessionID}");
        }

        public void OnLogout(SessionID sessionID)
        {
            lock (_lock)
            {
                _sessionID = null;
            }
            _logger.LogInformation($"[FIX-INITIATOR] Logout: {sessionID}");
        }

        public void FromAdmin(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            _logger.LogInformation($"[FIX-INITIATOR-ADMIN] MsgType={msgType}");
        }

        public void FromApp(Message msg, SessionID sessionID)
        {
            try
            {
                string msgType = msg.Header.GetString(Tags.MsgType);
                _logger.LogInformation($"[FIX-INITIATOR] 📨 FromApp called with MsgType={msgType}");
                
                if (msgType.Equals("8"))  // ExecutionReport
                {
                    string clOrdID = msg.GetString(11);
                    string ordID = msg.GetString(37);
                    string execType = msg.GetString(150);
                    string ordStatus = msg.GetString(39);
                    string text = msg.IsSetField(58) ? msg.GetString(58) : "";

                    var report = new ExecutionReportData
                    {
                        ClOrdID = clOrdID,
                        OrderID = ordID,
                        ExecType = execType,
                        OrdStatus = ordStatus,
                        IsAccepted = ordStatus == "2" || ordStatus == "0",  // FILLED or NEW
                        Text = text,
                        ReceivedAt = DateTime.UtcNow
                    };

                    _executionReports.Enqueue(report);
                    ExecutionReportReceived?.Invoke(this, report);
                    
                    _logger.LogInformation($"[FIX-INITIATOR] ✅ ExecutionReport received: {clOrdID} Status={ordStatus} IsAccepted={report.IsAccepted}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FIX-INITIATOR-ERROR] {ex.Message}");
            }
        }

        public void ToApp(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            string seqNum = msg.Header.GetString(Tags.MsgSeqNum);
            _logger.LogInformation($"[FIX-INITIATOR-TOAPP] 📤 Outbound app message:");
            _logger.LogInformation($"  MsgType={msgType} | SeqNum={seqNum}");
            _logger.LogInformation($"  Full: {msg}");
        }

        public void ToAdmin(Message msg, SessionID sessionID)
        {
            string msgType = msg.Header.GetString(Tags.MsgType);
            _logger.LogInformation($"[FIX-INITIATOR-TOADMIN] 📤 Outbound admin message: MsgType={msgType}");
        }
    }
}
