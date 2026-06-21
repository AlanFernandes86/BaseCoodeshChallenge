using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OrderGenerator.Domain.Entities;

namespace OrderGenerator.Application.Services
{
    /// <summary>
    /// Represents an order queued in the outbox when the FIX session is offline
    /// </summary>
    public class OutboxOrder
    {
        public string Symbol { get; set; } = "";
        public OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string ClOrdID { get; set; } = "";
        public string ConnectionId { get; set; } = "";
    }

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

    /// <summary>
    /// Shared state container for tracking pending orders and the outbox queue
    /// </summary>
    public interface IOrderTracker
    {
        ConcurrentQueue<OutboxOrder> OutboxQueue { get; }
        string GenerateClientOrderID();
    }
}
