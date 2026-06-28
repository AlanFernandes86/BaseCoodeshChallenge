using Microsoft.Extensions.Logging;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Domain.Services;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Store;
using QuickFix.Transport;
using System;

namespace OrderGenerator.Infrastructure.FIX
{
    /// <summary>
    /// Manages FIX Initiator connection to the OrderAccumulator
    /// Sends NewOrderSingle messages and receives ExecutionReports
    /// </summary>
    public class FIXInitiatorService : IApplication, IOrderSubmissionService, IFIXSessionManager
    {
        private SessionID? _sessionID;
        private SocketInitiator? _initiator;
        private readonly object _lock = new object();
        private readonly ILogger<FIXInitiatorService> _logger;
        
        public event EventHandler<ExecutionReportData>? ExecutionReportReceived;

        public bool IsReady
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
        public void SendNewOrderSingle(string symbol, string side, decimal quantity, decimal price, string clOrdID)
        {
            if (_sessionID == null)
            {
                throw new InvalidOperationException("FIX session not established");
            }

            try
            {
                _logger.LogInformation($"[FIX-INITIATOR] 📝 Building NewOrderSingle:");

                var order = new Message();                

                order.Header.SetField(new StringField(35, "D"));  // MsgType = NewOrderSingle

                string verifyMsgType = order.Header.GetString(Tags.MsgType);                

                order.SetField(new StringField(11, clOrdID));      // ClOrdID
                order.SetField(new StringField(55, symbol));       // Symbol
                
                string fixSide = side.Equals(nameof(OrderSide.Buy), StringComparison.OrdinalIgnoreCase) ? OrderSide.Buy : OrderSide.Sell;
                order.SetField(new StringField(54, fixSide));         // Side: 1=Buy, 2=Sell
                order.SetField(new DecimalField(38, quantity));    // OrderQty
                order.SetField(new DecimalField(44, price));       // Price
                order.SetField(new StringField(40, "2"));          // OrdType = 2 (Limit)
                order.SetField(new StringField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));  // TransactTime (field 60)

                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool sendResult = Session.SendToTarget(order, _sessionID);
                sw.Stop();
                OrderGenerator.Application.Services.PerformanceMonitor.Record("1. FIX_SendToTarget", sw.ElapsedTicks);

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
                
                if (msgType.Equals("8"))
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
                        IsAccepted = ordStatus == "0",
                        Text = text,
                        ReceivedAt = DateTime.UtcNow
                    };

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
