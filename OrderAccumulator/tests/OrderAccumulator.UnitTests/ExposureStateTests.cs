using System;
using FluentAssertions;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using Xunit;

namespace OrderAccumulator.UnitTests
{
    public class ExposureStateTests
    {
        [Fact]
        public void Constructor_ShouldInitializeAllValidSymbolsToZeroExposure()
        {
            // Arrange & Act
            var state = new ExposureState();

            // Assert
            foreach (var symbol in Constants.ValidSymbols)
            {
                state.GetExposure(symbol).Should().Be(0m);
            }
        }

        [Fact]
        public void GetExposure_WhenSymbolIsInvalid_ShouldReturnZero()
        {
            // Arrange
            var state = new ExposureState();

            // Act
            decimal exposure = state.GetExposure("UNKNOWN");

            // Assert
            exposure.Should().Be(0m);
        }

        [Fact]
        public void TryProcessOrder_WhenBuyOrder_ShouldIncreaseExposureAndReturnAccepted()
        {
            // Arrange
            var state = new ExposureState();
            var order = new OrderInfo("VALE3", Constants.OrderSide.Buy, 1000m, 50.00m, "CLT-1");

            // Act
            ProcessingResult result = state.TryProcessOrder(order);

            // Assert
            result.Should().NotBeNull();
            result.Accepted.Should().BeTrue();
            result.ResultingExposure.Should().Be(50000m);
            result.RejectReason.Should().BeNull();
            result.OrderID.Should().StartWith("ORD-").And.EndWith("000001");

            state.GetExposure("VALE3").Should().Be(50000m);
        }

        [Fact]
        public void TryProcessOrder_WhenSellOrder_ShouldDecreaseExposureAndReturnAccepted()
        {
            // Arrange
            var state = new ExposureState();
            var order = new OrderInfo("PETR4", Constants.OrderSide.Sell, 500m, 40.00m, "CLT-2");

            // Act
            ProcessingResult result = state.TryProcessOrder(order);

            // Assert
            result.Should().NotBeNull();
            result.Accepted.Should().BeTrue();
            result.ResultingExposure.Should().Be(-20000m);
            result.RejectReason.Should().BeNull();
            result.OrderID.Should().StartWith("ORD-").And.EndWith("000001");

            state.GetExposure("PETR4").Should().Be(-20000m);
        }

        [Fact]
        public void TryProcessOrder_WhenOrderBreachesPositiveLimit_ShouldBeRejectedAndNotChangeExposure()
        {
            // Arrange
            var state = new ExposureState();
            
            // Limit is 100M, let's send a Buy order of 100,000,001
            var order = new OrderInfo("VIIA4", Constants.OrderSide.Buy, 100_000_001m, 1.00m, "CLT-3");

            // Act
            ProcessingResult result = state.TryProcessOrder(order);

            // Assert
            result.Should().NotBeNull();
            result.Accepted.Should().BeFalse();
            result.RejectReason.Should().Contain("Exposure limit exceeded");
            result.ResultingExposure.Should().Be(0m); // Keeps current exposure
            result.OrderID.Should().StartWith("ORD-").And.EndWith("000001");

            state.GetExposure("VIIA4").Should().Be(0m);
        }

        [Fact]
        public void TryProcessOrder_WhenOrderBreachesNegativeLimit_ShouldBeRejectedAndNotChangeExposure()
        {
            // Arrange
            var state = new ExposureState();
            
            // Limit is -100M, let's send a Sell order of 100,000,001
            var order = new OrderInfo("VIIA4", Constants.OrderSide.Sell, 100_000_001m, 1.00m, "CLT-4");

            // Act
            ProcessingResult result = state.TryProcessOrder(order);

            // Assert
            result.Should().NotBeNull();
            result.Accepted.Should().BeFalse();
            result.RejectReason.Should().Contain("Exposure limit exceeded");
            result.ResultingExposure.Should().Be(0m); // Keeps current exposure

            state.GetExposure("VIIA4").Should().Be(0m);
        }

        [Fact]
        public void TryProcessOrder_ShouldIncrementOrderIdCounterForEveryAttempt()
        {
            // Arrange
            var state = new ExposureState();
            var order1 = new OrderInfo("VALE3", Constants.OrderSide.Buy, 1000m, 50.00m, "CLT-5");
            var order2 = new OrderInfo("VALE3", Constants.OrderSide.Buy, 100_000_001m, 1.00m, "CLT-6"); // Exceeds limit
            var order3 = new OrderInfo("VALE3", Constants.OrderSide.Sell, 500m, 50.00m, "CLT-7");

            // Act
            var res1 = state.TryProcessOrder(order1);
            var res2 = state.TryProcessOrder(order2);
            var res3 = state.TryProcessOrder(order3);

            // Assert
            res1.OrderID.Should().EndWith("000001");
            res2.OrderID.Should().EndWith("000002");
            res3.OrderID.Should().EndWith("000003");
        }

        [Fact]
        public void ApplyHistoricalExposure_ShouldUpdateStateAndIncrementCounterWithoutLimitCheck()
        {
            // Arrange
            var state = new ExposureState();

            // Act: Apply 150M exposure which exceeds standard 100M limit
            state.ApplyHistoricalExposure("PETR4", 150_000_000m);

            // Assert
            state.GetExposure("PETR4").Should().Be(150_000_000m);

            // Verify order ID counter was incremented by trying a new order
            var order = new OrderInfo("PETR4", Constants.OrderSide.Sell, 100m, 10.00m, "CLT-8");
            var res = state.TryProcessOrder(order);
            res.OrderID.Should().EndWith("000002"); // First was ApplyHistoricalExposure (1), second is this (2)
        }

        [Fact]
        public void AdvanceOrderIdCounter_ShouldIncrementOrderIdCounter()
        {
            // Arrange
            var state = new ExposureState();

            // Act
            state.AdvanceOrderIdCounter();

            // Assert: Try a new order, should have ID end with 000002
            var order = new OrderInfo("VALE3", Constants.OrderSide.Buy, 100m, 10.00m, "CLT-9");
            var res = state.TryProcessOrder(order);
            res.OrderID.Should().EndWith("000002");
        }
    }
}
