using System;

namespace OrderGenerator.WebAPI.Models
{
    public class SubmitOrderResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public string? ClOrdId { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
