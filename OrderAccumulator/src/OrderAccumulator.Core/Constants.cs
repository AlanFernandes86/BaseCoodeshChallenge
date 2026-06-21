namespace OrderAccumulator.Core
{
    /// <summary>
    /// Business constants: valid symbols, exposure limit, order sides
    /// </summary>
    public static class Constants
    {
        // Valid symbols for trading
        public static readonly string[] ValidSymbols = { "PETR4", "VALE3", "VIIA4" };

        // Maximum exposure limit per symbol: ±R$ 100,000,000
        public const decimal ExposureLimitReais = 100_000_000m;

        // Order side enumeration
        public static class OrderSide
        {
            public const char Buy = '1';   // FIX: Side = 1
            public const char Sell = '2';  // FIX: Side = 2
        }

        // FIX ExecutionType values
        public static class FIXExecType
        {
            public const char NEW = '0';       // New order accepted
            public const char PARTIAL_FILL = '1';
            public const char FILL = '2';
            public const char DONE_FOR_DAY = '3';
            public const char CANCELED = '4';
            public const char REPLACED = '5';
            public const char PENDING_CANCEL = '6';
            public const char STOPPED = '7';
            public const char REJECTED = '8';  // Order rejected
            public const char SUSPENDED = '9';
            public const char PENDING_NEW = 'A';
            public const char CALCULATED = 'B';
            public const char EXPIRED = 'C';
            public const char RESTATED = 'D';
            public const char PENDING_REPLACE = 'E';
            public const char TRADE = 'F';
            public const char TRADE_CORRECT = 'G';
            public const char TRADE_CANCEL = 'H';
            public const char ORDER_STATUS = 'I';
        }

        // FIX OrdStatus values
        public static class FIXOrdStatus
        {
            public const char NEW = '0';
            public const char PARTIALLY_FILLED = '1';
            public const char FILLED = '2';
            public const char DONE_FOR_DAY = '3';
            public const char CANCELED = '4';
            public const char PENDING_CANCEL = '6';
            public const char STOPPED = '7';
            public const char REJECTED = '8';
            public const char SUSPENDED = '9';
            public const char PENDING_NEW = 'A';
            public const char CALCULATED = 'B';
            public const char EXPIRED = 'C';
            public const char ACCEPTED_FOR_BIDDING = 'D';
            public const char PENDING_REPLACE = 'E';
            public const char REPLACED = 'F';
        }
    }
}
