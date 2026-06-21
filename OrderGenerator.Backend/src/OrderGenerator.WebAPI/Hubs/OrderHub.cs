using Microsoft.AspNetCore.SignalR;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Application.Services;

namespace OrderGenerator.WebAPI.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time order submission and execution reports.
    /// Clients invoke SubmitOrder and receive the result via the OrderResult event.
    /// </summary>
    public class OrderHub : Hub
    {
        private static readonly string[] ValidSymbols = ["PETR4", "VALE3", "VIIA4"];

        private readonly SubmitOrderUseCase _submissionService;
        private readonly IOrderTracker _orderTracker;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(
            SubmitOrderUseCase submissionService,
            IOrderTracker orderTracker,
            ILogger<OrderHub> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[SIGNALR] Client connected: {Context.ConnectionId}");
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
            Console.WriteLine($"[SIGNALR] SubmitOrder from {Context.ConnectionId}: {symbol} {side} qty={quantity} px={price}");

            // Server-side validation
            var validationError = Validate(symbol, side, quantity, price);
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

            // Parse side enum
            if (!Enum.TryParse<OrderSide>(side, ignoreCase: true, out var orderSide))
            {
                await Clients.Caller.SendAsync("OrderResult", new
                {
                    success = false,
                    status = "ERROR",
                    error = $"Lado inválido: '{side}'. Use 'Buy' ou 'Sell'."
                });
                return;
            }

            try
            {
                var result = await _submissionService.SubmitOrderAsync(
                    symbol,
                    orderSide,
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
                Console.WriteLine($"[SIGNALR-ERROR] SubmitOrder failed: {ex.Message}");
                await Clients.Caller.SendAsync("OrderResult", new
                {
                    success = false,
                    status = "ERROR",
                    error = $"Erro interno ao processar ordem: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Server-side business rules validation (mirrors frontend validation).
        /// </summary>
        private static string? Validate(string symbol, string side, decimal quantity, decimal price)
        {
            if (!Array.Exists(ValidSymbols, s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
                return $"Símbolo inválido: '{symbol}'. Use PETR4, VALE3 ou VIIA4.";

            if (!side.Equals("Buy", StringComparison.OrdinalIgnoreCase) &&
                !side.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                return $"Lado inválido: '{side}'. Use 'Buy' ou 'Sell'.";

            if (quantity <= 0 || quantity >= 100_000 || quantity != Math.Floor(quantity))
                return "Quantidade deve ser um inteiro positivo menor que 100.000.";

            if (price <= 0 || price >= 1000)
                return "Preço deve ser um valor positivo menor que 1.000.";

            // Check multiple of 0.01 — compare rounded value with original
            if (Math.Round(price, 2) != price)
                return "Preço deve ser múltiplo de 0,01.";

            return null;
        }
    }
}
