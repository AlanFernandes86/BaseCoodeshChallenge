using System;
using QuickFix;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;

namespace OrderAccumulator.Infrastructure.FIX;

/// <summary>
/// Placeholder for FIX message conversion.
/// Will implement full parsing/building once QuickFIXn.Core 1.14.1 API is researched.
/// </summary>
public class FIXMessageConverter
{
    public OrderInfo ParseNewOrderSingle(Message message)
    {
        throw new NotImplementedException("FIX message parsing deferred to API research phase");
    }

    public Message BuildExecutionReport(Message incomingOrder, ProcessingResult result, string execID)
    {
        throw new NotImplementedException("FIX message building deferred to API research phase");
    }
}
