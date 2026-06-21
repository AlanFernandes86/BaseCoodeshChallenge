using OrderGenerator.Domain.Entities;
using OrderGenerator.WebAPI.Models;

namespace OrderGenerator.WebAPI.Validators
{
    public static class OrderValidator
    {
        private static readonly string[] ValidSymbols = ["PETR4", "VALE3", "VIIA4"];

        public static string? Validate(string symbol, string side, decimal quantity, decimal price)
        {
            if (string.IsNullOrWhiteSpace(symbol) ||
                !Array.Exists(ValidSymbols, s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
                return $"Símbolo inválido: '{symbol}'. Use PETR4, VALE3 ou VIIA4.";

            if (string.IsNullOrWhiteSpace(side) ||
                (!side.Equals(nameof(OrderSide.Buy), StringComparison.OrdinalIgnoreCase) &&
                 !side.Equals(nameof(OrderSide.Sell), StringComparison.OrdinalIgnoreCase)))
                return $"Lado inválido: '{side}'. Use '{nameof(OrderSide.Buy)}' ou '{nameof(OrderSide.Sell)}'.";

            if (quantity <= 0 || quantity >= 100_000 || quantity != Math.Floor(quantity))
                return "Quantidade deve ser um inteiro positivo menor que 100.000.";

            if (price <= 0 || price >= 1000)
                return "Preço deve ser um valor positivo menor que 1.000.";

            if (Math.Round(price, 2) != price)
                return "Preço deve ser múltiplo de 0,01.";

            return null;
        }

        public static string? Validate(SubmitOrderRequest request)
        {
            if (request == null) return "Request não pode ser nulo.";
            return Validate(request.Symbol, request.Side, request.Quantity, request.Price);
        }
    }
}
