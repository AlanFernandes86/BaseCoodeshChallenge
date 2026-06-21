using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuickFix;
using OrderAccumulator.Core;
using OrderAccumulator.Infrastructure.FIX;
using OrderAccumulator.Services;

namespace OrderAccumulator
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║   OrderAccumulator - FIX Acceptor      ║");
            Console.WriteLine("╚════════════════════════════════════════╝\n");

            try
            {
                var builder = Host.CreateApplicationBuilder(args);

                string fixConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fix.cfg");
                if (!File.Exists(fixConfigPath))
                {
                    throw new FileNotFoundException($"FIX configuration not found at {fixConfigPath}");
                }

                var settings = new SessionSettings(fixConfigPath);
                builder.Services.AddSingleton(settings);
                Console.WriteLine($"✅ FIX config loaded: {fixConfigPath}\n");

                builder.Services.AddSingleton<ExposureState>();
                Console.WriteLine("✅ ExposureState registered\n");

                builder.Services.AddSingleton<IApplication, FIXAcceptorApplication>();
                Console.WriteLine("✅ FIXAcceptorApplication registered as IApplication\n");

                builder.Services.AddSingleton<FIXStoreExposureReconstructor>();
                Console.WriteLine("✅ FIXStoreExposureReconstructor registered\n");

                builder.Services.AddHostedService<FIXAcceptorHostedService>();
                Console.WriteLine("✅ FIXAcceptorHostedService registered as HostedService\n");

                var host = builder.Build();
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FATAL: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
