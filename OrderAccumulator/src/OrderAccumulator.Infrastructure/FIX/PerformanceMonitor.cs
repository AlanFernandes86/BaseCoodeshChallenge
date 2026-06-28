using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OrderAccumulator.Infrastructure.FIX
{
    public static class PerformanceMonitor
    {
        private static long _totalTicks = 0;
        private static long _count = 0;

        static PerformanceMonitor()
        {
            // Inicia uma thread em background que imprime as estatísticas a cada 5 segundos
            // sem bloquear o processamento das ordens!
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    long totalTicks = Interlocked.Read(ref _totalTicks);
                    long count = Interlocked.Read(ref _count);

                    if (count > 0)
                    {
                        // Converte ticks para milissegundos
                        double avgMs = (double)totalTicks / count / TimeSpan.TicksPerMillisecond;
                        Console.WriteLine($"[METRICS] FromApp - Ordens processadas: {count} | Tempo Médio: {avgMs:F4} ms");
                    }
                }
            });
        }

        public static void Record(long elapsedTicks)
        {
            Interlocked.Add(ref _totalTicks, elapsedTicks);
            Interlocked.Increment(ref _count);
        }
    }
}
