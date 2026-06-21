using System;
using QuickFix;
using QuickFix.Fields;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using static OrderAccumulator.Core.Constants;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// Converts between FIX 4.4 Protocol messages and Domain objects.
    /// Uses QuickFIXn generic Message API with field tags.
    /// </summary>
    public class FIXMessageConverter
    {
        // FIX 4.4 Field Tags
        private const int FIELD_SYMBOL = 55;         // Symbol
        private const int FIELD_SIDE = 54;           // Side
        private const int FIELD_ORDERQTY = 38;       // OrderQty
        private const int FIELD_PRICE = 44;          // Price
        private const int FIELD_CLORDID = 11;        // ClOrdID
        private const int FIELD_ORDERID = 37;        // OrderID
        private const int FIELD_EXECID = 17;         // ExecID
        private const int FIELD_EXECTYPE = 150;      // ExecType
        private const int FIELD_ORDSTATUS = 39;      // OrdStatus
        private const int FIELD_LEAVESQTY = 151;     // LeavesQty
        private const int FIELD_CUMQTY = 14;         // CumQty
        private const int FIELD_AVGPX = 6;           // AvgPx
        private const int FIELD_TRANSACTTIME = 60;   // TransactTime
        private const int FIELD_TEXT = 58;           // Text
        private const int FIELD_MSGTYPE = 35;        // MsgType

        /// <summary>
        /// Parse a NewOrderSingle FIX message into domain OrderInfo object.
        /// Extracts fields 55 (Symbol), 54 (Side), 38 (OrderQty), 44 (Price), 11 (ClOrdID).
        /// </summary>
        public OrderInfo ParseNewOrderSingle(QuickFix.Message message)
        {
            try
            {
                // Extract required fields using generic GetString/GetDecimal API
                string symbol = message.GetString(FIELD_SYMBOL);           // Field 55
                char sideChar = message.GetString(FIELD_SIDE)[0];          // Field 54
                decimal quantity = message.GetDecimal(FIELD_ORDERQTY);     // Field 38
                decimal price = message.GetDecimal(FIELD_PRICE);           // Field 44
                string clOrdID = message.GetString(FIELD_CLORDID);         // Field 11

                // Validate symbol
                if (!Constants.ValidSymbols.Contains(symbol))
                {
                    throw new ArgumentException($"Invalid symbol: {symbol}. Valid: {string.Join(", ", Constants.ValidSymbols)}");
                }

                // Convert side char to enum
                OrderSide side = sideChar switch
                {
                    '1' => OrderSide.Buy,    // FIX 4.4 Buy
                    '2' => OrderSide.Sell,   // FIX 4.4 Sell
                    _ => throw new ArgumentException($"Invalid side: {sideChar}. Expected '1' (Buy) or '2' (Sell)")
                };

                // Return domain object
                return new OrderInfo
                {
                    Symbol = symbol,
                    Side = side,
                    Quantity = quantity,
                    Price = price,
                    ClOrdID = clOrdID
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse NewOrderSingle: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Build an ExecutionReport FIX message as response to an order.
        /// Sets all required fields per FIX 4.4 spec.
        /// </summary>
        public QuickFix.Message BuildExecutionReport(QuickFix.Message incomingOrder, ProcessingResult result, string execID)
        {
            try
            {
                // Create new Message for ExecutionReport
                var execReport = new QuickFix.Message();
                
                // Set header
                execReport.Header.SetField(new StringField(FIELD_MSGTYPE, "8"));  // MsgType = ExecutionReport

                // Extract original order data
                string symbol = incomingOrder.GetString(FIELD_SYMBOL);
                char side = incomingOrder.GetString(FIELD_SIDE)[0];
                decimal orderQty = incomingOrder.GetDecimal(FIELD_ORDERQTY);
                string clOrdID = incomingOrder.GetString(FIELD_CLORDID);

                // Determine exec type and ord status based on acceptance
                string execType = result.Accepted ? "0" : "8";         // '0'=NEW, '8'=REJECTED
                string ordStatus = result.Accepted ? "0" : "8";        // '0'=NEW, '8'=REJECTED

                // Set required fields
                execReport.SetField(new StringField(FIELD_ORDERID, result.OrderID));          // Field 37
                execReport.SetField(new StringField(FIELD_EXECID, execID));                   // Field 17
                execReport.SetField(new StringField(FIELD_EXECTYPE, execType));               // Field 150
                execReport.SetField(new StringField(FIELD_ORDSTATUS, ordStatus));             // Field 39
                execReport.SetField(new StringField(FIELD_SYMBOL, symbol));                   // Field 55
                execReport.SetField(new StringField(FIELD_SIDE, side.ToString()));            // Field 54
                execReport.SetField(new DecimalField(FIELD_LEAVESQTY, orderQty));             // Field 151
                execReport.SetField(new DecimalField(FIELD_CUMQTY, 0));                       // Field 14
                execReport.SetField(new DecimalField(FIELD_AVGPX, 0));                        // Field 6
                execReport.SetField(new DecimalField(FIELD_ORDERQTY, orderQty));              // Field 38
                execReport.SetField(new DecimalField(FIELD_PRICE, incomingOrder.GetDecimal(FIELD_PRICE))); // Field 44

                // Set optional fields
                execReport.SetField(new StringField(FIELD_CLORDID, clOrdID));                 // Field 11
                execReport.SetField(new StringField(FIELD_TRANSACTTIME, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));  // Field 60
                
                // Set status message (expose or reason)
                string statusMsg = result.Accepted 
                    ? $"Order accepted. Symbol={symbol}, Exposure={result.ResultingExposure:C}"
                    : $"Order rejected. {result.RejectReason}";
                execReport.SetField(new StringField(FIELD_TEXT, statusMsg));  // Field 58

                return execReport;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to build ExecutionReport: {ex.Message}", ex);
            }
        }
    }
}
