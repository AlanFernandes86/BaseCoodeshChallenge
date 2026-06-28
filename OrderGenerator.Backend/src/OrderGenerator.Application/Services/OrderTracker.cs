using System;
using System.Collections.Concurrent;
using System.Threading;
using OrderGenerator.Application.DTOs;

namespace OrderGenerator.Application.Services
{
    public class OrderTracker : IOrderTracker
    {
        private long _orderCounter = 0;

        public ConcurrentQueue<OutboxOrder> OutboxQueue { get; } = new();

        public string GenerateClientOrderID(string connectionId)
        {
            long orderId = Interlocked.Increment(ref _orderCounter);
            return $"CLT-{DateTime.UtcNow:yyyyMMdd}-{orderId:D6}_{connectionId}";
        }
    }
}
