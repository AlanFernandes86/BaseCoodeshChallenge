using FluentAssertions;
using OrderGenerator.WebAPI.Models;
using OrderGenerator.WebAPI.Validators;
using Xunit;

namespace OrderGenerator.UnitTests.Validators
{
    public class OrderValidatorTests
    {
        [Theory]
        [InlineData("PETR4", "Buy", 100, 25.50)]
        [InlineData("VALE3", "Sell", 1, 0.01)]
        [InlineData("VIIA4", "buy", 99999, 999.99)]
        public void Validate_WithValidInputs_ShouldReturnNull(string symbol, string side, decimal quantity, decimal price)
        {
            // Act
            string? result = OrderValidator.Validate(symbol, side, quantity, price);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("INVALID3")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Validate_WithInvalidSymbol_ShouldReturnErrorMessage(string? symbol)
        {
            // Act
            string? result = OrderValidator.Validate(symbol!, "Buy", 100, 20.00m);

            // Assert
            result.Should().NotBeNull().And.Contain("Símbolo inválido");
        }

        [Theory]
        [InlineData("InvalidSide")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Validate_WithInvalidSide_ShouldReturnErrorMessage(string? side)
        {
            // Act
            string? result = OrderValidator.Validate("PETR4", side!, 100, 20.00m);

            // Assert
            result.Should().NotBeNull().And.Contain("Lado inválido");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(100_000)]
        [InlineData(100.5)] // Decimal quantity
        public void Validate_WithInvalidQuantity_ShouldReturnErrorMessage(decimal quantity)
        {
            // Act
            string? result = OrderValidator.Validate("PETR4", "Buy", quantity, 20.00m);

            // Assert
            result.Should().NotBeNull().And.Contain("Quantidade deve ser um inteiro positivo");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(1000)]
        public void Validate_WithInvalidPriceBoundary_ShouldReturnErrorMessage(decimal price)
        {
            // Act
            string? result = OrderValidator.Validate("PETR4", "Buy", 100, price);

            // Assert
            result.Should().NotBeNull().And.Contain("Preço deve ser um valor positivo menor que 1.000");
        }

        [Fact]
        public void Validate_WithPriceNotMultipleOfCent_ShouldReturnErrorMessage()
        {
            // Act
            string? result = OrderValidator.Validate("PETR4", "Buy", 100, 10.123m);

            // Assert
            result.Should().NotBeNull().And.Contain("Preço deve ser múltiplo de 0,01");
        }

        [Fact]
        public void Validate_WithNullRequest_ShouldReturnErrorMessage()
        {
            // Act
            string? result = OrderValidator.Validate(null!);

            // Assert
            result.Should().NotBeNull().And.Contain("Request não pode ser nulo");
        }

        [Fact]
        public void Validate_WithValidRequestObject_ShouldReturnNull()
        {
            // Arrange
            var request = new SubmitOrderRequest
            {
                Symbol = "VALE3",
                Side = "Sell",
                Quantity = 500,
                Price = 62.45m
            };

            // Act
            string? result = OrderValidator.Validate(request);

            // Assert
            result.Should().BeNull();
        }
    }
}
