using System;
using System.Threading;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using QuickFix;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// FIX message handler
    /// </summary>
    public class FIXAcceptorHandler : IDisposable
    {
        private const int Capacity = 1024;
        private const int Mask = Capacity - 1;
        private readonly RingBufferEvent[] _ringBuffer = new RingBufferEvent[Capacity];
        
        private readonly ExposureState _exposureState;
        
        private long _producerSequence = -1;
        private long _consumerSequence = -1;
        
        private readonly Thread _consumerThread;
        private volatile bool _isRunning = true;
        
        private static int _execIDCounter = 0;

        public FIXAcceptorHandler(ExposureState exposureState)
        {
            _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
            
            _consumerThread = new Thread(ConsumeLoop)
            {
                IsBackground = true,
                Name = "RingBufferConsumer",
                Priority = ThreadPriority.Highest
            };
            _consumerThread.Start();
        }

        public void Produce(OrderInfo orderInfo, Message msg, SessionID sessionID)
        {
            long nextSequence = Interlocked.Increment(ref _producerSequence);
            
            while (nextSequence - Volatile.Read(ref _consumerSequence) >= Capacity)
            {
                Thread.SpinWait(10);
                Console.WriteLine("Waiting for buffer to be ready");
            }

            int index = (int)(nextSequence & Mask);
            
            _ringBuffer[index].OrderInfo = orderInfo;
            _ringBuffer[index].OriginalMessage = msg;
            _ringBuffer[index].SessionID = sessionID;
            
            Volatile.Write(ref _ringBuffer[index].IsReadyToConsume, true);
        }

        private void ConsumeLoop()
        {
            long nextExpectedSequence = 0;
            SpinWait spinWait = new SpinWait();

            while (_isRunning)
            {
                int index = (int)(nextExpectedSequence & Mask);
                
                if (!Volatile.Read(ref _ringBuffer[index].IsReadyToConsume))
                {
                    spinWait.SpinOnce();
                    continue;
                }
                
                spinWait.Reset();

                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                var orderInfo = _ringBuffer[index].OrderInfo;
                var result = _exposureState.TryProcessOrder(orderInfo);
                
                string execID = $"EXEC-{DateTime.UtcNow:yyyyMMdd}-{++_execIDCounter:D6}";
                var execReport = _ringBuffer[index].OriginalMessage.ToExecutionReport(result, execID);
                var sessionId = _ringBuffer[index].SessionID;
                
                // Dispara o I/O pesado de disco para a ThreadPool, 
                // liberando a thread consumidora para processar a próxima ordem na memória em nanossegundos!
                System.Threading.ThreadPool.QueueUserWorkItem(_ => 
                {
                    Session.SendToTarget(execReport, sessionId);
                });
                
                sw.Stop();
                PerformanceMonitor.Record(sw.ElapsedTicks);

                Volatile.Write(ref _ringBuffer[index].IsReadyToConsume, false);
                Volatile.Write(ref _consumerSequence, nextExpectedSequence);
                nextExpectedSequence++;
            }
        }

        public ExposureState GetExposureState() => _exposureState;

        public void Dispose()
        {
            _isRunning = false;
            _consumerThread.Join(1000); // Give it a second to shutdown gracefully
        }
    }
}
