using System;
using QuickFix;
using QuickFix.Fields;
using OrderAccumulator.Core.Models;
using OrderAccumulator.Core;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// Extension methods for QuickFIX/n Message class.
    /// </summary>
    public static class FIXMessageExtensions
    {
        /// <summary>
        /// Converts a NewOrderSingle (MsgType=D) FIX message to a domain OrderInfo object.
        /// </summary>
        public static OrderInfo ToOrderInfo(this Message message)
        {
            try
            {
                return new OrderInfo
                (
                    message.GetString(Tags.Symbol),
                    message.GetChar(Tags.Side),
                    message.GetDecimal(Tags.OrderQty),
                    message.GetDecimal(Tags.Price),
                    message.GetString(Tags.ClOrdID)
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert Message to OrderInfo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Builds an ExecutionReport message response from an incoming NewOrderSingle message.
        /// </summary>
        public static Message ToExecutionReport(this Message incomingOrder, ProcessingResult result, string execID)
        {
            try
            {
                var execReport = new Message();
                
                // Set MsgType = ExecutionReport (8)
                execReport.Header.SetField(new StringField(Tags.MsgType, "8"));

                string symbol = incomingOrder.GetString(Tags.Symbol);
                char side = incomingOrder.GetString(Tags.Side)[0];
                decimal orderQty = incomingOrder.GetDecimal(Tags.OrderQty);
                string clOrdID = incomingOrder.GetString(Tags.ClOrdID);

                string execType = result.Accepted ? Constants.FIXExecType.NEW.ToString() : Constants.FIXExecType.REJECTED.ToString();
                string ordStatus = result.Accepted ? Constants.FIXOrdStatus.NEW.ToString() : Constants.FIXOrdStatus.REJECTED.ToString();

                execReport.SetField(new StringField(Tags.OrderID, result.OrderID));          // Field 37
                execReport.SetField(new StringField(Tags.ExecID, execID));                   // Field 17
                execReport.SetField(new StringField(Tags.ExecType, execType));               // Field 150
                execReport.SetField(new StringField(Tags.OrdStatus, ordStatus));             // Field 39
                execReport.SetField(new StringField(Tags.Symbol, symbol));                   // Field 55
                execReport.SetField(new StringField(Tags.Side, side.ToString()));            // Field 54
                execReport.SetField(new DecimalField(Tags.LeavesQty, orderQty));             // Field 151
                execReport.SetField(new DecimalField(Tags.CumQty, 0));                       // Field 14
                execReport.SetField(new DecimalField(Tags.AvgPx, 0));                        // Field 6
                execReport.SetField(new DecimalField(Tags.OrderQty, orderQty));              // Field 38
                execReport.SetField(new DecimalField(Tags.Price, incomingOrder.GetDecimal(Tags.Price))); // Field 44

                execReport.SetField(new StringField(Tags.ClOrdID, clOrdID));                 // Field 11
                execReport.SetField(new StringField(Tags.TransactTime, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));  // Field 60
                
                string statusMsg = result.Accepted 
                    ? $"Order accepted. Symbol={symbol}, Exposure={result.ResultingExposure:C}"
                    : $"Order rejected. {result.RejectReason}";
                execReport.SetField(new StringField(Tags.Text, statusMsg));  // Field 58

                return execReport;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to build ExecutionReport from Message: {ex.Message}", ex);
            }
        }
    }
}
