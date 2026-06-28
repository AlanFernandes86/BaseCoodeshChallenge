using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OrderGenerator.Application.Services
{
    public static class PerformanceMonitor
    {
        private class Metric
        {
            public long TotalTicks;
            public long Count;
        }

        private static readonly ConcurrentDictionary<string, Metric> _metrics = new();

        static PerformanceMonitor()
        {
            // Thread para exibir os gargalos do backend no terminal a cada 5 segundos
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    
                    if (_metrics.IsEmpty) continue;

                    Console.WriteLine("\n[METRICS] === BACKEND PERFORMANCE ===");
                    foreach (var kvp in _metrics)
                    {
                        long totalTicks = Interlocked.Read(ref kvp.Value.TotalTicks);
                        long count = Interlocked.Read(ref kvp.Value.Count);

                        if (count > 0)
                        {
                            double avgMs = (double)totalTicks / count / TimeSpan.TicksPerMillisecond;
                            Console.WriteLine($"[METRICS] {kvp.Key,-30} | Ordens: {count,-8} | Média: {avgMs:F4} ms");
                        }
                    }
                    Console.WriteLine("=====================================\n");
                }
            });
        }

        public static void Record(string metricName, long elapsedTicks)
        {
            var metric = _metrics.GetOrAdd(metricName, _ => new Metric());
            Interlocked.Add(ref metric.TotalTicks, elapsedTicks);
            Interlocked.Increment(ref metric.Count);
        }
    }
}
