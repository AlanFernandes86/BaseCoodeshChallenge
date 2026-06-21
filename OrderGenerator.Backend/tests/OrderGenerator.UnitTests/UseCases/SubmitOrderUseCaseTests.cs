using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderGenerator.Application.DTOs;
using OrderGenerator.Application.Services;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Domain.Services;
using Xunit;

namespace OrderGenerator.UnitTests.UseCases
{
    public class SubmitOrderUseCaseTests
    {
        private readonly Mock<IOrderSubmissionService> _mockSubmissionService;
        private readonly Mock<IOrderTracker> _mockOrderTracker;
        private readonly Mock<ILogger<SubmitOrderUseCase>> _mockLogger;
        private readonly SubmitOrderUseCase _useCase;

        public SubmitOrderUseCaseTests()
        {
            _mockSubmissionService = new Mock<IOrderSubmissionService>();
            _mockOrderTracker = new Mock<IOrderTracker>();
            _mockLogger = new Mock<ILogger<SubmitOrderUseCase>>();

            // Setup default client order ID generation
            _mockOrderTracker.Setup(x => x.GenerateClientOrderID()).Returns("CLT-20260621-000001");
            _mockOrderTracker.Setup(x => x.OutboxQueue).Returns(new System.Collections.Concurrent.ConcurrentQueue<OutboxOrder>());

            _useCase = new SubmitOrderUseCase(
                _mockSubmissionService.Object,
                _mockOrderTracker.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public void Constructor_WithNullArguments_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SubmitOrderUseCase(null!, _mockOrderTracker.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SubmitOrderUseCase(_mockSubmissionService.Object, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SubmitOrderUseCase(_mockSubmissionService.Object, _mockOrderTracker.Object, null!));
        }

        [Fact]
        public async Task SubmitOrderAsync_WhenFixSessionOffline_ShouldAddToOutboxQueueAndReturnPending()
        {
            // Arrange
            _mockSubmissionService.Setup(x => x.IsReady).Returns(false);

            var submitDto = new SubmitOrder
            {
                Symbol = "PETR4",
                Side = "Buy",
                Quantity = 500,
                Price = 30.50m,
                ConnectionId = "conn-1"
            };

            // Act
            OrderSubmissionResult result = await _useCase.SubmitOrderAsync(submitDto);

            // Assert
            result.Should().NotBeNull();
            result.ClOrdID.Should().Be("CLT-20260621-000001");
            result.IsAccepted.Should().BeFalse();
            result.OrdStatus.Should().Be("PENDING");
            result.Message.Should().Contain("enfileirada pois o servidor de exposição está offline");
            result.ConnectionId.Should().Be("conn-1");

            // Verify added to outbox
            _mockOrderTracker.Object.OutboxQueue.Count.Should().Be(1);
            _mockOrderTracker.Object.OutboxQueue.TryPeek(out var outboxOrder).Should().BeTrue();
            outboxOrder.Should().NotBeNull();
            outboxOrder!.ClOrdID.Should().Be("CLT-20260621-000001");
            outboxOrder.Symbol.Should().Be("PETR4");

            // Verify FIX Send was never called
            _mockSubmissionService.Verify(x => x.SendNewOrderSingle(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SubmitOrderAsync_WhenFixSessionOnline_ShouldSendDirectlyAndReturnPending()
        {
            // Arrange
            _mockSubmissionService.Setup(x => x.IsReady).Returns(true);

            var submitDto = new SubmitOrder
            {
                Symbol = "VALE3",
                Side = "Sell",
                Quantity = 1000,
                Price = 65.00m,
                ConnectionId = "conn-2"
            };

            // Act
            OrderSubmissionResult result = await _useCase.SubmitOrderAsync(submitDto);

            // Assert
            result.Should().NotBeNull();
            result.ClOrdID.Should().Be("CLT-20260621-000001");
            result.IsAccepted.Should().BeFalse();
            result.OrdStatus.Should().Be("PENDING");
            result.Message.Should().Contain("Ordem enviada para o servidor de exposição");
            result.ConnectionId.Should().Be("conn-2");

            // Verify nothing added to outbox
            _mockOrderTracker.Object.OutboxQueue.Count.Should().Be(0);

            // Verify FIX Send was called once with exact arguments
            _mockSubmissionService.Verify(x => x.SendNewOrderSingle(
                "VALE3", "Sell", 1000, 65.00m, "CLT-20260621-000001"), Times.Once);
        }

        [Fact]
        public async Task SubmitOrderAsync_WhenFixSendThrowsException_ShouldReturnRejectedResult()
        {
            // Arrange
            _mockSubmissionService.Setup(x => x.IsReady).Returns(true);
            _mockSubmissionService.Setup(x => x.SendNewOrderSingle(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .Throws(new Exception("FIX session dropped during transmission"));

            var submitDto = new SubmitOrder
            {
                Symbol = "VALE3",
                Side = "Sell",
                Quantity = 1000,
                Price = 65.00m,
                ConnectionId = "conn-3"
            };

            // Act
            OrderSubmissionResult result = await _useCase.SubmitOrderAsync(submitDto);

            // Assert
            result.Should().NotBeNull();
            result.ClOrdID.Should().Be("CLT-20260621-000001");
            result.IsAccepted.Should().BeFalse();
            result.OrdStatus.Should().Be("8");
            result.Message.Should().Contain("FIX submission error: FIX session dropped during transmission");
            result.ConnectionId.Should().BeEmpty();

            // Verify nothing added to outbox
            _mockOrderTracker.Object.OutboxQueue.Count.Should().Be(0);
        }
    }
}
