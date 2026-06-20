using System;
using System.IO;
using System.Threading;
using QuickFix;
using QuickFix.Store;
using OrderAccumulator.Core;
using OrderAccumulator.Infrastructure.FIX;

namespace OrderAccumulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ThreadedSocketAcceptor acceptor = null;

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
                Console.WriteLine();

                // Load FIX configuration from bin directory (copied during build)
                string fixConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fix.cfg");
                
                if (!File.Exists(fixConfigPath))
                {
                    throw new FileNotFoundException($"FIX configuration not found at {fixConfigPath}");
                }

                var settings = new SessionSettings(fixConfigPath);
                Console.WriteLine($"✅ FIX config loaded: {fixConfigPath}\n");

                // Initialize FIX Acceptor application (MessageCracker for typed message dispatch)
                var application = new FIXAcceptorApplication(exposureState);
                Console.WriteLine("✅ FIXAcceptorApplication initialized\n");

                // Create message store factory (persistent for session sequence numbers)
                var storeFactory = new FileStoreFactory(settings);
                
                // Create and start the FIX acceptor on port 9999 (no explicit logger)
                acceptor = new ThreadedSocketAcceptor(application, storeFactory, settings, (QuickFix.Logger.ILogFactory)null);
                acceptor.Start();
                Console.WriteLine("✅ FIX Acceptor started on port 9999\n");

                Console.WriteLine("📡 Ready. Waiting for FIX connections...\n");
                Console.WriteLine("Press Ctrl+C to exit gracefully.\n");

                // Keep the application running
                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (ConfigError ex)
            {
                Console.WriteLine($"❌ FIX ConfigError: {ex.Message}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FATAL: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                Environment.Exit(1);
            }
            finally
            {
                if (acceptor != null)
                {
                    try
                    {
                        acceptor.Stop();
                        Console.WriteLine("\n✅ Acceptor stopped gracefully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error stopping acceptor: {ex.Message}");
                    }
                }
            }
        }
    }
}
