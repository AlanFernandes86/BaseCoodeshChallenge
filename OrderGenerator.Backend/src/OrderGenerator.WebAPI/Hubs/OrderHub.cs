using Microsoft.AspNetCore.SignalR;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Application.Services;
using OrderGenerator.WebAPI.Validators;

namespace OrderGenerator.WebAPI.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time order submission and execution reports.
    /// Clients invoke SubmitOrder and receive the result via the OrderResult event.
    /// </summary>
    public class OrderHub : Hub
    {
        private readonly SubmitOrderUseCase _submissionService;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(
            SubmitOrderUseCase submissionService,
            IOrderTracker orderTracker,
            ILogger<OrderHub> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"[SIGNALR] Client connected: {Context.ConnectionId}");
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }
            

        /// Called when a client disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SIGNALR] Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Submits a new order via the FIX protocol and returns the execution result to the caller.
        /// Invoked by the client: hub.invoke("SubmitOrder", symbol, side, quantity, price)
        /// Result delivered to client via: on("OrderResult", callback)
        /// </summary>
        public async Task SubmitOrder(string symbol, string side, decimal quantity, decimal price)
        {
            _logger.LogInformation($"[SIGNALR] SubmitOrder from {Context.ConnectionId}: {symbol} {side} qty={quantity} px={price}");

            // Server-side validation
            var validationError = OrderValidator.Validate(symbol, side, quantity, price);
            if (validationError != null)
            {
                await Clients.Caller.SendAsync("OrderResult", new
                {
                    success = false,
                    status = "ERROR",
                    error = validationError
                });
                return;
            }

            try
            {
                var result = await _submissionService.SubmitOrderAsync(
                    symbol,
                    side,
                    quantity,
                    price,
                    connectionId: Context.ConnectionId
                );
                
                await Clients.Caller.SendAsync("OrderResult", new
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
                _logger.LogError($"[SIGNALR-ERROR] SubmitOrder failed: {ex.Message}");
                await Clients.Caller.SendAsync("OrderResult", new
                {
                    success = false,
                    status = "ERROR",
                    error = $"Erro interno ao processar ordem: {ex.Message}"
                });
            }
        }

    }
}
