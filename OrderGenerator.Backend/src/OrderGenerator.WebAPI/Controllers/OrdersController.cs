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
        private readonly ISubmitOrderUseCase _submissionService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(ISubmitOrderUseCase submissionService, ILogger<OrdersController> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _logger = logger;
        }

        /// <summary>
        /// Submit an order for processing - only sends the order to the FIX engine, does not wait for execution report
        /// Actually execution reports will only be sent via SignalR to the client
        /// </summary>
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitOrder([FromBody] SubmitOrderRequest request)
        {
            if (request == null)
            {
                return BadRequest(new SubmitOrderResponse { Success = false, Status = "ERROR", Error = "Request body is required" });
            }

            _logger.LogInformation("Received order submission request: {Symbol} {Side} {Quantity} @ {Price}",
                request.Symbol, request.Side, request.Quantity, request.Price);

            // Validate request
            var validationError = OrderValidator.Validate(request);
            if (validationError != null)
            {
                return BadRequest(new SubmitOrderResponse { Success = false, Status = "ERROR", Error = validationError });
            }

            try
            {
                // Submit order via FIX
                var result = await _submissionService.SubmitOrderAsync(new OrderGenerator.Application.DTOs.SubmitOrder
                {
                    Symbol = request.Symbol,
                    Side = request.Side,
                    Quantity = request.Quantity,
                    Price = request.Price
                });

                return Ok(new SubmitOrderResponse
                {
                    Success = true,
                    Status = result.OrdStatus,
                    ClOrdId = result.ClOrdID,
                    Message = result.Message,
                    Timestamp = result.ReceivedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SubmitOrderResponse
                {
                    Success = false,
                    Status = "ERROR",
                    Error = $"Order submission failed: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

    }
}
