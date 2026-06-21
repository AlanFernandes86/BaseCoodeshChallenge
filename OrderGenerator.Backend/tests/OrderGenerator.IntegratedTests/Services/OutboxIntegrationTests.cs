using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using OrderGenerator.Application.Services;
using OrderGenerator.IntegratedTests.Setup;
using OrderGenerator.WebAPI.Models;
using Xunit;

namespace OrderGenerator.IntegratedTests.Services
{
    public class OutboxIntegrationTests : IClassFixture<MockedWebApplicationFactory>
    {
        private readonly MockedWebApplicationFactory _factory;

        public OutboxIntegrationTests(MockedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SubmitOrder_WhenFixSessionOffline_AddsToOutboxQueue()
        {
            // Arrange
            // Force the FIX session to be offline
            _factory.MockFixService.Setup(x => x.IsReady).Returns(false);
            var client = _factory.CreateClient();
            var orderTracker = _factory.Services.GetRequiredService<IOrderTracker>();
            
            // Clear queue before testing
            orderTracker.OutboxQueue.Clear();

            var request = new SubmitOrderRequest
            {
                Symbol = "VIIA4",
                Side = "Sell",
                Quantity = 500,
                Price = 3.15m
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/orders/submit", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>();
            content.Should().NotBeNull();
            content!.Success.Should().BeTrue();
            content.Status.Should().Be("PENDING");
            content.Message.Should().Contain("enfileirada pois o servidor de exposição está offline");

            // Verify it was added to Outbox Queue
            orderTracker.OutboxQueue.Count.Should().BeGreaterThan(0);
            orderTracker.OutboxQueue.TryPeek(out var outboxOrder).Should().BeTrue();
            outboxOrder.Should().NotBeNull();
            outboxOrder!.Symbol.Should().Be("VIIA4");
            outboxOrder.Quantity.Should().Be(500);

            // Verify FIX service was NEVER called since it's offline
            _factory.MockFixService.Verify(x => x.SendNewOrderSingle(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>()), Moq.Times.Never);
        }
    }
}
