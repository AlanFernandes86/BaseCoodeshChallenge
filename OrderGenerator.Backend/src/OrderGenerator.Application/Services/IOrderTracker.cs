using System;
using System.Collections.Concurrent;

using OrderGenerator.Application.DTOs;

namespace OrderGenerator.Application.Services
{
    /// <summary>
    /// Shared state container for tracking pending orders and the outbox queue
    /// </summary>
    public interface IOrderTracker
    {
        ConcurrentQueue<OutboxOrder> OutboxQueue { get; }
        string GenerateClientOrderID();
    }
}
