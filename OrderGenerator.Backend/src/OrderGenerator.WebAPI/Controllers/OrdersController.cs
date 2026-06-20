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

        // Valid symbols as defined in the business requirements
        private static readonly string[] ValidSymbols = ["PETR4", "VALE3", "VIIA4"];

        /// <summary>
        /// Validate order submission request against all business rules.
        /// Mirrors the frontend validation so the API is independently safe.
        /// </summary>
        private static string? ValidateRequest(SubmitOrderRequest request)
        {
            // Symbol must be one of the allowed tickers
            if (string.IsNullOrWhiteSpace(request.Symbol) ||
                !Array.Exists(ValidSymbols, s => s.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase)))
                return $"Símbolo inválido: '{request.Symbol}'. Use PETR4, VALE3 ou VIIA4.";

            // Side must be Buy or Sell (case-insensitive)
            if (string.IsNullOrWhiteSpace(request.Side) ||
                (!request.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase) &&
                 !request.Side.Equals("Sell", StringComparison.OrdinalIgnoreCase)))
                return $"Lado inválido: '{request.Side}'. Use 'Buy' ou 'Sell'.";

            // Quantity: positive integer, strictly less than 100.000
            if (request.Quantity <= 0 || request.Quantity >= 100_000 || request.Quantity != Math.Floor(request.Quantity))
                return "Quantidade deve ser um inteiro positivo menor que 100.000.";

            // Price: positive decimal, less than 1.000, multiple of 0.01
            if (request.Price <= 0 || request.Price >= 1000)
                return "Preço deve ser um valor positivo menor que 1.000.";

            if (Math.Round(request.Price, 2) != request.Price)
                return "Preço deve ser múltiplo de 0,01.";

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

