using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OrderGenerator.IntegratedTests.Setup;
using OrderGenerator.WebAPI.Models;
using Xunit;

namespace OrderGenerator.IntegratedTests.Controllers
{
    public class OrdersControllerTests : IClassFixture<MockedWebApplicationFactory>
    {
        private readonly MockedWebApplicationFactory _factory;

        public OrdersControllerTests(MockedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SubmitOrder_WithInvalidSymbol_ReturnsBadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = new SubmitOrderRequest
            {
                Symbol = "INVALID3",
                Side = "Buy",
                Quantity = 100,
                Price = 10.5m
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/orders/submit", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var content = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>();
            content.Should().NotBeNull();
            content!.Success.Should().BeFalse();
            content.Status.Should().Be("ERROR");
            content.Error.Should().Contain("Símbolo inválido");
        }

        [Fact]
        public async Task SubmitOrder_WithInvalidQuantity_ReturnsBadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = new SubmitOrderRequest
            {
                Symbol = "PETR4",
                Side = "Sell",
                Quantity = -50, // Invalid
                Price = 25.5m
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/orders/submit", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var content = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>();
            content.Should().NotBeNull();
            content!.Success.Should().BeFalse();
            content.Status.Should().Be("ERROR");
            content.Error.Should().Contain("Quantidade deve ser um inteiro positivo");
        }

        [Fact]
        public async Task SubmitOrder_WithValidData_SessionReady_ReturnsPending()
        {
            // Arrange
            _factory.MockFixService.Setup(x => x.IsReady).Returns(true);
            var client = _factory.CreateClient();
            var request = new SubmitOrderRequest
            {
                Symbol = "VALE3",
                Side = "Buy",
                Quantity = 1000,
                Price = 50.25m
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/orders/submit", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>();
            content.Should().NotBeNull();
            content!.Success.Should().BeTrue();
            content.Status.Should().Be("PENDING");
            content.Message.Should().Contain("Ordem enviada para o servidor");

            // Verify FIX service was called
            _factory.MockFixService.Verify(x => x.SendNewOrderSingle(
                "VALE3", "Buy", 1000, 50.25m, It.IsAny<string>()), Moq.Times.Once);
        }
    }
}
