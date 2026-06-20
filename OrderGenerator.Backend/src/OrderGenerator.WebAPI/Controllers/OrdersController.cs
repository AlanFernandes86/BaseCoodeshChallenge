using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Application.UseCases;

namespace OrderGenerator.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderSubmissionService _submissionService;

        public OrdersController(OrderSubmissionService submissionService)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
        }

        /// <summary>
        /// Submit an order for processing
        /// </summary>
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitOrder([FromBody] SubmitOrderRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            // Validate request
            var validationError = ValidateRequest(request);
            if (validationError != null)
            {
                return BadRequest(new { error = validationError });
            }

            try
            {
                // Parse OrderSide
                if (!System.Enum.TryParse<OrderSide>(request.Side, ignoreCase: true, out var side))
                {
                    return BadRequest(new { error = $"Invalid side: {request.Side}. Use 'Buy' or 'Sell'" });
                }

                // Submit order via FIX
                var result = await _submissionService.SubmitOrderAsync(
                    request.Symbol,
                    side,
                    request.Quantity,
                    request.Price,
                    timeoutMs: 5000
                );

                if (result.IsAccepted)
                {
                    return Ok(new
                    {
                        success = true,
                        orderId = result.OrderID,
                        clOrdId = result.ClOrdID,
                        status = "ACCEPTED",
                        message = result.Message,
                        timestamp = result.ReceivedAt
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        clOrdId = result.ClOrdID,
                        status = "REJECTED",
                        message = result.Message,
                        timestamp = result.ReceivedAt
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = $"Order submission failed: {ex.Message}",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Validate order submission request
        /// </summary>
        private string? ValidateRequest(SubmitOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
                return "Symbol is required";

            if (string.IsNullOrWhiteSpace(request.Side))
                return "Side is required";

            if (request.Quantity <= 0)
                return "Quantity must be greater than 0";

            if (request.Price <= 0)
                return "Price must be greater than 0";

            return null;
        }
    }

    /// <summary>
    /// Request model for order submission
    /// </summary>
    public class SubmitOrderRequest
    {
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";  // "Buy" or "Sell"
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
    }
}

