using System;

namespace OrderGenerator.Domain.Entities
{  
    /// <summary>
    /// Order entity
    /// </summary>
    public class Order
    {
        public string OrderId { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "PENDING";
        public DateTime CreatedAt { get; set; }
    }
}
