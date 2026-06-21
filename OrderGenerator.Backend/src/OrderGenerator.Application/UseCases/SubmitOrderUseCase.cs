using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrderGenerator.Application.Services;
using OrderGenerator.Domain.Services;

using OrderGenerator.Application.DTOs;

namespace OrderGenerator.Application.UseCases
{
    public class SubmitOrderUseCase : ISubmitOrderUseCase
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

        public async Task<OrderSubmissionResult> SubmitOrderAsync(SubmitOrder dto)
        {
            string clOrdID = _orderTracker.GenerateClientOrderID();

            if (!_submissionService.IsReady)
            {
                var outboxOrder = new OutboxOrder
                {
                    ClOrdID = clOrdID,
                    Symbol = dto.Symbol,
                    Side = dto.Side,
                    Quantity = dto.Quantity,
                    Price = dto.Price,
                    ConnectionId = dto.ConnectionId
                };

                _orderTracker.OutboxQueue.Enqueue(outboxOrder);

                _logger.LogInformation($"[SUBMIT-OUTBOX] Session offline. Queued order {clOrdID} for {dto.Symbol}");

                return new OrderSubmissionResult
                {
                    ClOrdID = clOrdID,
                    IsAccepted = false,
                    OrdStatus = "PENDING",
                    Message = "A ordem foi enfileirada pois o servidor de exposição está offline. Será processada assim que a conexão for estabelecida.",
                    ReceivedAt = DateTime.UtcNow,
                    ConnectionId = dto.ConnectionId
                };
            }

            try
            {
                _submissionService.SendNewOrderSingle(dto.Symbol, dto.Side, dto.Quantity, dto.Price, clOrdID);

                return new OrderSubmissionResult
                {
                    ClOrdID = clOrdID,
                    IsAccepted = false,
                    OrdStatus = "PENDING",
                    Message = "Ordem enviada para o servidor de exposição.",
                    ReceivedAt = DateTime.UtcNow,
                    ConnectionId = dto.ConnectionId
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
