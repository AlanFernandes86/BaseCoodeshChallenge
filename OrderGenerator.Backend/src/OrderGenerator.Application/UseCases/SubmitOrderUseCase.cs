using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderGenerator.Application.Services;
using OrderGenerator.Domain.Entities;
using OrderGenerator.Domain.Services;

namespace OrderGenerator.Application.UseCases
{
    public class SubmitOrderUseCase
    {
        private readonly IOrderSubmissionService _submissionService;
        private readonly IOrderTracker _orderTracker;
        private readonly ILogger<SubmitOrderUseCase> _logger;

        public SubmitOrderUseCase(
            IOrderSubmissionService submissionService,
            IOrderTracker orderTracker,
            ILogger<SubmitOrderUseCase> logger)
        {
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
            _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OrderSubmissionResult> SubmitOrderAsync(
            string symbol,
            string side,
            decimal quantity,
            decimal price,
            string connectionId = "",
            int timeoutMs = 1500)
        {
            string clOrdID = _orderTracker.GenerateClientOrderID();

            if (!_submissionService.IsReady)
            {
                var outboxOrder = new OutboxOrder
                {
                    Symbol = symbol,
                    Side = side,
                    Quantity = quantity,
                    Price = price,
                    ClOrdID = clOrdID,
                    ConnectionId = connectionId
                };

                _orderTracker.OutboxQueue.Enqueue(outboxOrder);

                _logger.LogInformation($"[SUBMIT-OUTBOX] Session offline. Queued order {clOrdID} for {symbol}");

                return new OrderSubmissionResult
                {
                    ClOrdID = clOrdID,
                    IsAccepted = false,
                    OrdStatus = "PENDING",
                    Message = "A ordem foi enfileirada pois o servidor de exposição está offline. Será processada assim que a conexão for estabelecida.",
                    ReceivedAt = DateTime.UtcNow,
                    ConnectionId = connectionId
                };
            }

            try
            {
                _submissionService.SendNewOrderSingle(symbol, side, quantity, price, clOrdID);

                return new OrderSubmissionResult
                {
                    ClOrdID = clOrdID,
                    IsAccepted = false,
                    OrdStatus = "PENDING",
                    Message = "Ordem enviada para o servidor de exposição.",
                    ReceivedAt = DateTime.UtcNow,
                    ConnectionId = connectionId
                };
            }
            catch (Exception ex)
            {
                return OrderSubmissionResult.CreateRejected(
                    clOrdID,
                    $"FIX submission error: {ex.Message}"
                );
            }
        }
    }
}
