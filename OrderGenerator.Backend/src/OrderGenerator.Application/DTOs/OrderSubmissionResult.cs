using System;

namespace OrderGenerator.Application.DTOs
{
    /// <summary>
    /// Result of order submission attempt
    /// </summary>
    public class OrderSubmissionResult
    {
        public string ClOrdID { get; set; } = "";
        public string OrderID { get; set; } = "";
        public bool IsAccepted { get; set; }
        public string ExecType { get; set; } = "";
        public string OrdStatus { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
        public string ConnectionId { get; set; } = "";

        public static OrderSubmissionResult CreateRejected(string clOrdID, string message)
        {
            return new OrderSubmissionResult
            {
                ClOrdID = clOrdID,
                IsAccepted = false,
                Message = message,
                ReceivedAt = DateTime.UtcNow,
                OrdStatus = "8"  // REJECTED
            };
        }
    }
}
