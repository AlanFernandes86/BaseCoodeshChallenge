namespace OrderAccumulator.Core.Models
{
    /// <summary>
    /// Parsed order information extracted from FIX NewOrderSingle message
    /// Simple POCO, no validation (trust that input is well-formed)
    /// </summary>
    public class OrderInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public Constants.OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string ClOrdID { get; set; } = string.Empty;  // Client Order ID from FIX

        /// <summary>
        /// Calculates the exposure impact of this order
        /// Buy: +qty * price
        /// Sell: -qty * price
        /// </summary>
        public decimal CalculateExposureImpact()
        {
            return Side == Constants.OrderSide.Buy
                ? Quantity * Price
                : -Quantity * Price;
        }

        public override string ToString() =>
            $"OrderInfo {{ Symbol={Symbol}, Side={Side}, Qty={Quantity}, Price={Price}, ClOrdID={ClOrdID} }}";
    }
}
