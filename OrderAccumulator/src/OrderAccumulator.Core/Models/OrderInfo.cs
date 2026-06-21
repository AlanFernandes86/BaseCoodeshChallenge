namespace OrderAccumulator.Core.Models
{
    /// <summary>
    /// Parsed order information extracted from FIX NewOrderSingle message.
    /// </summary>
    public readonly record struct OrderInfo(
        string Symbol,
        char Side,
        decimal Quantity,
        decimal Price,
        string ClOrdID)
    {
        /// <summary>
        /// Calculates the exposure impact of this order.
        /// Buy: +qty * price
        /// Sell: -qty * price
        /// </summary>
        public decimal CalculateExposureImpact() =>
            Side == Constants.OrderSide.Buy ? Quantity * Price : -Quantity * Price;
    }
}