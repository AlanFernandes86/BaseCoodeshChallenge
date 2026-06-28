using OrderAccumulator.Core.Models;
using QuickFix;

namespace OrderAccumulator.Infrastructure.FIX
{
    public struct RingBufferEvent
    {
        public OrderInfo OrderInfo;
        public Message OriginalMessage;
        public SessionID SessionID;
        public volatile bool IsReadyToConsume;
    }
}
