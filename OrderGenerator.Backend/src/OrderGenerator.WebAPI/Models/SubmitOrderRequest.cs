namespace OrderGenerator.WebAPI.Models
{
    public class SubmitOrderRequest
    {
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";  // "Buy" or "Sell"
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
