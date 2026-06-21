using FluentAssertions;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using Xunit;

namespace OrderAccumulator.UnitTests.Models
{
    public class OrderInfoTests
    {
        [Fact]
        public void CalculateExposureImpact_WhenBuyOrder_ShouldReturnPositiveImpact()
        {
            // Arrange
            var order = new OrderInfo(
                Symbol: "VALE3",
                Side: Constants.OrderSide.Buy,
                Quantity: 1000m,
                Price: 50.00m,
                ClOrdID: "CLT-12345"
            );

            // Act
            decimal impact = order.CalculateExposureImpact();

            // Assert
            impact.Should().Be(50000m);
        }

        [Fact]
        public void CalculateExposureImpact_WhenSellOrder_ShouldReturnNegativeImpact()
        {
            // Arrange
            var order = new OrderInfo(
                Symbol: "VALE3",
                Side: Constants.OrderSide.Sell,
                Quantity: 500m,
                Price: 60.00m,
                ClOrdID: "CLT-67890"
            );

            // Act
            decimal impact = order.CalculateExposureImpact();

            // Assert
            impact.Should().Be(-30000m);
        }
    }
}
