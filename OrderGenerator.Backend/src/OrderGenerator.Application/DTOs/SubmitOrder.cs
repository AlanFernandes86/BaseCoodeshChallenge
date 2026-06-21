namespace OrderGenerator.Application.DTOs
{
    public class SubmitOrder
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string ConnectionId { get; set; } = string.Empty;
    }
}
