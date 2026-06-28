using Microsoft.AspNetCore.SignalR;
using OrderGenerator.Application.UseCases;
using OrderGenerator.WebAPI.Validators;
using OrderGenerator.WebAPI.Models;

namespace OrderGenerator.WebAPI.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time order submission and execution reports.
    /// Clients invoke SubmitOrder and receive the result via the OrderResult event.
    /// </summary>
    public class OrderHub : Hub
    {
        private readonly ISubmitOrderUseCase _submissionService;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(
            ISubmitOrderUseCase submissionService,
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
        /// Submits a new order via the FIX protocol
        /// Invoked by the client: hub.invoke("SubmitOrder", symbol, side, quantity, price)
        /// </summary>
        public async Task SubmitOrder(string symbol, string side, decimal quantity, decimal price)
        {
            _logger.LogInformation($"[SIGNALR] SubmitOrder from {Context.ConnectionId}: {symbol} {side} qty={quantity} px={price}");

            // Server-side validation
            var validationError = OrderValidator.Validate(symbol, side, quantity, price);
            if (validationError != null)
            {
                await Clients.Caller.SendAsync("OrderResult", new SubmitOrderResponse
                {
                    Success = false,
                    Status = "ERROR",
                    Error = validationError
                });
                return;
            }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await _submissionService.SubmitOrderAsync(new OrderGenerator.Application.DTOs.SubmitOrder
                {
                    Symbol = symbol,
                    Side = side,
                    Quantity = quantity,
                    Price = price,
                    ConnectionId = Context.ConnectionId
                });
                
                sw.Stop();
                OrderGenerator.Application.Services.PerformanceMonitor.Record("0. SignalR_Incoming_Total", sw.ElapsedTicks);

                await Clients.Caller.SendAsync("OrderResult", new SubmitOrderResponse
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
                _logger.LogError($"[SIGNALR-ERROR] SubmitOrder failed: {ex.Message}");
                await Clients.Caller.SendAsync("OrderResult", new SubmitOrderResponse
                {
                    Success = false,
                    Status = "ERROR",
                    Error = $"Erro interno ao processar ordem: {ex.Message}"
                });
            }
        }

    }
}
