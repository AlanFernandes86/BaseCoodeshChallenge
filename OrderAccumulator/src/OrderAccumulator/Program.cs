using System;
using System.IO;
using System.Threading;
using OrderAccumulator.Core;
using QuickFix;

namespace OrderAccumulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("╔════════════════════════════════════════╗");
                Console.WriteLine("║   OrderAccumulator - FIX Acceptor      ║");
                Console.WriteLine("╚════════════════════════════════════════╝\n");

                // Initialize domain state (pure, thread-safe exposure tracking)
                var exposureState = new ExposureState();
                Console.WriteLine("✅ ExposureState initialized\n");

                // Print welcome message
                exposureState.PrintState();
                Console.WriteLine("📡 Service ready (FIX bootstrap in PHASE 3)\n");
                Console.WriteLine("Press Ctrl+C to exit gracefully.\n");

                // Keep the application running
                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine($"📋 StackTrace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
