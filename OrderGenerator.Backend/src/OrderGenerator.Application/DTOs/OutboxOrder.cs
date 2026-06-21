namespace OrderGenerator.Application.DTOs
{
    /// <summary>
    /// Represents an order queued in the outbox when the FIX session is offline
    /// </summary>
    public class OutboxOrder
    {
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string ClOrdID { get; set; } = "";
        public string ConnectionId { get; set; } = "";
    }
}
