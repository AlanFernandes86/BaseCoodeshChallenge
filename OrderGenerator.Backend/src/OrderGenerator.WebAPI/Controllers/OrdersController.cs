using Microsoft.AspNetCore.Mvc;
using OrderGenerator.Application.UseCases;
using OrderGenerator.WebAPI.Models;
using OrderGenerator.WebAPI.Validators;

namespace OrderGenerator.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly SubmitOrderUseCase _submissionService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(SubmitOrderUseCase submissionService, ILogger<OrdersController> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _logger = logger;
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

            _logger.LogInformation("Received order submission request: {Symbol} {Side} {Quantity} @ {Price}",
                request.Symbol, request.Side, request.Quantity, request.Price);

            // Validate request
            var validationError = OrderValidator.Validate(request);
            if (validationError != null)
            {
                return BadRequest(new { error = validationError });
            }

            try
            {
                // Submit order via FIX
                var result = await _submissionService.SubmitOrderAsync(
                    request.Symbol,
                    request.Side,
                    request.Quantity,
                    request.Price,
                    timeoutMs: 5000
                );

                return Ok(new
                {
                    success = true,
                    status = result.OrdStatus,
                    clOrdId = result.ClOrdID,
                    message = result.Message,
                    timestamp = result.ReceivedAt
                });                
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

    }
}

