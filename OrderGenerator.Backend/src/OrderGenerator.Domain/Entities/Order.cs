using System;

namespace OrderGenerator.Domain.Entities
{
    /// <summary>
    /// Order side enumeration
    /// </summary>
    public enum OrderSide
    {
        /// <summary>
        /// Buy order (FIX field 54 = '1')
        /// </summary>
        Buy = 1,

        /// <summary>
        /// Sell order (FIX field 54 = '2')
        /// </summary>
        Sell = 2
    }

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
