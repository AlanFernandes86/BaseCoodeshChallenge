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
        /// Send a NewOrderSingle via FIX protocol
        /// </summary>
        void SendNewOrderSingle(string symbol, Entities.OrderSide side, decimal quantity, decimal price, string clOrdID);

        /// <summary>
        /// Try to get an execution report response (non-blocking)
        /// </summary>
        bool TryGetExecutionReport(out ExecutionReportData? report);
    }
}
