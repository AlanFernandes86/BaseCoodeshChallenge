using System;
using OrderGenerator.Domain.Entities;

namespace OrderGenerator.Domain.Services
{
    /// <summary>
    /// Result of execution report received from FIX
    /// </summary>
    public class ExecutionReportData
    {
        public string ClOrdID { get; set; } = "";
        public string OrderID { get; set; } = "";
        public string ExecType { get; set; } = "";
        public string OrdStatus { get; set; } = "";
        public bool IsAccepted { get; set; }
        public string Text { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
    }

    /// <summary>
    /// Interface for FIX order submission service
    /// </summary>
    public interface IFIXOrderSubmissionService
    {
        /// <summary>
        /// Event fired when an ExecutionReport is received from FIX
        /// </summary>
        event EventHandler<ExecutionReportData>? ExecutionReportReceived;

        /// <summary>
        /// Gets whether the FIX session is currently active
        /// </summary>
        bool IsSessionActive { get; }

        /// <summary>
        /// Send a NewOrderSingle via FIX protocol
        /// </summary>
        void SendNewOrderSingle(string symbol, Entities.OrderSide side, decimal quantity, decimal price, string clOrdID);

        /// <summary>
        /// Try to get an execution report response (non-blocking)
        /// </summary>
        bool TryGetExecutionReport(out ExecutionReportData? report);

        /// <summary>
        /// Starts the FIX initiator with the given configuration file path. This method should establish the FIX session and be ready to send/receive messages.
        /// </summary>
        /// <param name="configFilePath"></param>
        void Start(string configFilePath);
    }
}
