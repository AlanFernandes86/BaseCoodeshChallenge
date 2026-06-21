namespace OrderGenerator.Domain.Entities
{
    /// <summary>
    /// Order side enumeration
    /// </summary>
    public static class OrderSide
    {
        /// <summary>
        /// Buy order (FIX field 54 = '1')
        /// </summary>
        public const string Buy = "1";

        /// <summary>
        /// Sell order (FIX field 54 = '2')
        /// </summary>
        public const string Sell = "2";
    }
}
