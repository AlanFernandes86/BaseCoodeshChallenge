using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Store;
using QuickFix.Logger;
using QuickFix.Transport;
using OrderAccumulator.Core;
using OrderAccumulator.Infrastructure.FIX;
using Xunit;

namespace OrderAccumulator.IntegratedTests
{
    public class IntegratedTests : IDisposable
    {
        private const string AcceptorCfg1 = "fix_test_acceptor_1.cfg";
        private const string InitiatorCfg1 = "fix_test_initiator_1.cfg";
        
        private const string AcceptorCfg2 = "fix_test_acceptor_2.cfg";
        private const string InitiatorCfg2 = "fix_test_initiator_2.cfg";

        public IntegratedTests()
        {
            CleanAllDirectories();
            
            // Programmatically generate configuration files for both test environments
            CreateConfigFile(AcceptorCfg1, "acceptor", "127.0.0.1", 10003, "./store_acceptor_1", "./log_acceptor_1", "ACCUMULATOR", "BACKEND");
            CreateConfigFile(InitiatorCfg1, "initiator", "127.0.0.1", 10003, "./store_initiator_1", "./log_initiator_1", "BACKEND", "ACCUMULATOR");
            
            CreateConfigFile(AcceptorCfg2, "acceptor", "127.0.0.1", 10004, "./store_acceptor_2", "./log_acceptor_2", "ACCUMULATOR", "BACKEND");
            CreateConfigFile(InitiatorCfg2, "initiator", "127.0.0.1", 10004, "./store_initiator_2", "./log_initiator_2", "BACKEND", "ACCUMULATOR");
        }

        public void Dispose()
        {
            CleanAllDirectories();
        }

        private static void CleanAllDirectories()
        {
            CleanDirectory("./store_acceptor_1");
            CleanDirectory("./log_acceptor_1");
            CleanDirectory("./store_initiator_1");
            CleanDirectory("./log_initiator_1");
            
            CleanDirectory("./store_acceptor_2");
            CleanDirectory("./log_acceptor_2");
            CleanDirectory("./store_initiator_2");
            CleanDirectory("./log_initiator_2");

            DeleteFile(AcceptorCfg1);
            DeleteFile(InitiatorCfg1);
            DeleteFile(AcceptorCfg2);
            DeleteFile(InitiatorCfg2);
        }

        private static void CleanDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST-CLEANUP] ⚠️ Failed to clean directory {path}: {ex.Message}");
            }
        }

        private static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST-CLEANUP] ⚠️ Failed to delete file {path}: {ex.Message}");
            }
        }

        private static void CreateConfigFile(string path, string connectionType, string host, int port, string storePath, string logPath, string compId, string targetId)
        {
            var content = $@"
                [DEFAULT]
                ConnectionType={connectionType}
                FileStorePath={storePath}
                FileLogPath={logPath}
                ResetOnLogon=N
                ResetOnLogout=N
                ResetOnDisconnect=N
                ValidateIncomingMessage=N
                ValidateOutgoingMessage=N
                StartTime=00:00:00
                EndTime=23:59:59
                HeartBtInt=30

                [SESSION]
                BeginString=FIX.4.4
                SenderCompID={compId}
                TargetCompID={targetId}
                DataDictionary=FIX44.xml
                UseDataDictionary=Y
                {(connectionType == "acceptor" ? $"SocketAcceptHost={host}\nSocketAcceptPort={port}" : $"SocketConnectHost={host}\nSocketConnectPort={port}\nReconnectInterval=1")}
            ";
            File.WriteAllText(path, content.Trim());
        }

        [Fact]
        public async Task Test_NormalFlow_And_LimitEnforcement()
        {
            // ── 1. SETUP SUT (Accumulator 1: ExposureState + FIX Acceptor + Fake Web API) ──
            var exposureState = new ExposureState();

            // Setup ASP.NET Core Web API (Fake API) to expose exposure state
            var apiBuilder = WebApplication.CreateBuilder();
            apiBuilder.Services.AddSingleton(exposureState);
            var apiApp = apiBuilder.Build();
            apiApp.Urls.Add("http://127.0.0.1:5001");

            apiApp.MapGet("/api/exposure/{symbol}", (string symbol, ExposureState state) =>
            {
                return Results.Ok(state.GetExposure(symbol));
            });

            await apiApp.StartAsync();

            // Start FIX Acceptor
            var acceptorSettings = new SessionSettings(AcceptorCfg1);
            var acceptorApp = new FIXAcceptorApplication(exposureState);
            var acceptorStoreFactory = new FileStoreFactory(acceptorSettings);
            var acceptorLogFactory = new FileLogFactory(acceptorSettings);
            var acceptor = new ThreadedSocketAcceptor(acceptorApp, acceptorStoreFactory, acceptorSettings, acceptorLogFactory);
            
            acceptor.Start();

            // ── 2. SETUP SIMULATED INITIATOR (Fake Backend client) ──
            var initiatorSettings = new SessionSettings(InitiatorCfg1);
            var initiatorApp = new TestInitiatorApplication();
            var initiatorStoreFactory = new FileStoreFactory(initiatorSettings);
            var initiatorLogFactory = new FileLogFactory(initiatorSettings);
            var initiator = new SocketInitiator(initiatorApp, initiatorStoreFactory, initiatorSettings, initiatorLogFactory);

            initiator.Start();

            try
            {
                // Wait for FIX Logon session to be established
                await initiatorApp.WaitForLogonAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(initiatorApp.Session);

                // ── 3. TEST NORMAL ORDER (Buy 100 shares of PETR4 @ R$ 10.0 = R$ 1,000 exposure) ──
                var order = new QuickFix.Message();
                order.Header.SetField(new StringField(35, "D")); // MsgType=D (NewOrderSingle)
                order.SetField(new StringField(11, "CLT-TEST-000001")); // ClOrdID
                order.SetField(new StringField(55, "PETR4")); // Symbol
                order.SetField(new StringField(54, "1")); // Side=Buy
                order.SetField(new DecimalField(38, 100m)); // OrderQty
                order.SetField(new DecimalField(44, 10.0m)); // Price
                order.SetField(new StringField(40, "2")); // OrdType=Limit
                order.SetField(new StringField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss"))); // TransactTime

                Session.SendToTarget(order, initiatorApp.Session);

                // Query the Accumulator's Fake API to verify exposure update
                using var httpClient = new HttpClient();
                decimal petr4Exposure = 0m;
                bool apiReflectedNewState = false;

                for (int i = 0; i < 50; i++)
                {
                    var response = await httpClient.GetAsync("http://127.0.0.1:5001/api/exposure/PETR4");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        petr4Exposure = decimal.Parse(content, CultureInfo.InvariantCulture);
                        if (petr4Exposure == 1000m)
                        {
                            apiReflectedNewState = true;
                            break;
                        }
                    }
                    await Task.Delay(100);
                }

                Assert.True(apiReflectedNewState, $"Expected exposure was R$ 1,000.00 but got R$ {petr4Exposure:N2} via Fake API");

                // ── 4. TEST EXPOSURE LIMIT (Order exceeding R$ 100,000,000 limit) ──
                // R$ 100,000,001 should trigger rejection
                var giantOrder = new QuickFix.Message();
                giantOrder.Header.SetField(new StringField(35, "D"));
                giantOrder.SetField(new StringField(11, "CLT-TEST-000002"));
                giantOrder.SetField(new StringField(55, "PETR4"));
                giantOrder.SetField(new StringField(54, "1"));
                giantOrder.SetField(new DecimalField(38, 1m));
                giantOrder.SetField(new DecimalField(44, 100_000_001m)); // Price
                giantOrder.SetField(new StringField(40, "2")); // OrdType=Limit
                giantOrder.SetField(new StringField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));

                Session.SendToTarget(giantOrder, initiatorApp.Session);

                // Wait slightly and verify exposure remains R$ 1,000
                await Task.Delay(500);
                var verifyResponse = await httpClient.GetStringAsync("http://127.0.0.1:5001/api/exposure/PETR4");
                Assert.Equal(1000m, decimal.Parse(verifyResponse, CultureInfo.InvariantCulture));
            }
            finally
            {
                // Clean shutdown of the first host session
                initiator.Stop();
                initiator.Dispose();
                acceptor.Stop();
                acceptor.Dispose();
                await apiApp.StopAsync();
                await apiApp.DisposeAsync();
            }
        }

        [Fact]
        public async Task Test_CrashRecovery_ReconstructsExposureFromStore()
        {
            // ── 1. SETUP SUT (Accumulator 2: ExposureState + FIX Acceptor + Fake Web API) ──
            var exposureState = new ExposureState();

            var apiBuilder = WebApplication.CreateBuilder();
            apiBuilder.Services.AddSingleton(exposureState);
            var apiApp = apiBuilder.Build();
            apiApp.Urls.Add("http://127.0.0.1:5002");

            apiApp.MapGet("/api/exposure/{symbol}", (string symbol, ExposureState state) =>
            {
                return Results.Ok(state.GetExposure(symbol));
            });

            await apiApp.StartAsync();

            var acceptorSettings = new SessionSettings(AcceptorCfg2);
            var acceptorApp = new FIXAcceptorApplication(exposureState);
            var acceptorStoreFactory = new FileStoreFactory(acceptorSettings);
            var acceptorLogFactory = new FileLogFactory(acceptorSettings);
            var acceptor = new ThreadedSocketAcceptor(acceptorApp, acceptorStoreFactory, acceptorSettings, acceptorLogFactory);
            
            acceptor.Start();

            // ── 2. SETUP SIMULATED INITIATOR (Fake Backend client) ──
            var initiatorSettings = new SessionSettings(InitiatorCfg2);
            var initiatorApp = new TestInitiatorApplication();
            var initiatorStoreFactory = new FileStoreFactory(initiatorSettings);
            var initiatorLogFactory = new FileLogFactory(initiatorSettings);
            var initiator = new SocketInitiator(initiatorApp, initiatorStoreFactory, initiatorSettings, initiatorLogFactory);

            initiator.Start();

            try
            {
                await initiatorApp.WaitForLogonAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(initiatorApp.Session);

                // ── 3. SEND ORDER TO ACCUMULATE (Buy 200 shares @ R$ 15.0 = R$ 3,000 PETR4) ──
                var order = new QuickFix.Message();
                order.Header.SetField(new StringField(35, "D"));
                order.SetField(new StringField(11, "CLT-TEST-RECOVER-01"));
                order.SetField(new StringField(55, "PETR4"));
                order.SetField(new StringField(54, "1"));
                order.SetField(new DecimalField(38, 200m));
                order.SetField(new DecimalField(44, 15.0m));
                order.SetField(new StringField(40, "2"));
                order.SetField(new StringField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));

                Session.SendToTarget(order, initiatorApp.Session);

                // Wait for the exposure to be populated
                using var httpClient = new HttpClient();
                decimal petr4Exposure = 0m;
                bool apiReflectedNewState = false;

                for (int i = 0; i < 50; i++)
                {
                    var response = await httpClient.GetAsync("http://127.0.0.1:5002/api/exposure/PETR4");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        petr4Exposure = decimal.Parse(content, CultureInfo.InvariantCulture);
                        if (petr4Exposure == 3000m)
                        {
                            apiReflectedNewState = true;
                            break;
                        }
                    }
                    await Task.Delay(100);
                }

                Assert.True(apiReflectedNewState, "Pre-shutdown exposure was not correctly recorded.");
            }
            finally
            {
                // Crash the accumulator and client session (shut down the host)
                initiator.Stop();
                initiator.Dispose();
                acceptor.Stop();
                acceptor.Dispose();
                await apiApp.StopAsync();
                await apiApp.DisposeAsync();
            }

            // Give resources a moment to release file handles and socket bindings
            await Task.Delay(1000);

            // ── 4. RECONSTRUCT STATE (Start a new clean instance pointing to the same FileStore) ──
            var cleanState = new ExposureState();
            var reconstructSettings = new SessionSettings(AcceptorCfg2);
            var reconstructor = new FIXStoreExposureReconstructor();
            
            int reconstructedCount = reconstructor.Reconstruct(cleanState, reconstructSettings);

            // Verify that the accepted execution report was parsed and reconstructed
            Assert.Equal(1, reconstructedCount);
            Assert.Equal(3000m, cleanState.GetExposure("PETR4"));

            // ── 5. START A NEW HOST WITH RECONSTRUCTED STATE AND EXPOSE IT ──
            var newApiBuilder = WebApplication.CreateBuilder();
            var newApiApp = newApiBuilder.Build();
            newApiApp.Urls.Add("http://127.0.0.1:5002");
            newApiApp.MapGet("/api/exposure/{symbol}", (string symbol) =>
            {
                return Results.Ok(cleanState.GetExposure(symbol));
            });

            await newApiApp.StartAsync();

            try
            {
                using var httpClient = new HttpClient();
                var apiResponse = await httpClient.GetStringAsync("http://127.0.0.1:5002/api/exposure/PETR4");
                Assert.Equal(3000m, decimal.Parse(apiResponse, CultureInfo.InvariantCulture));
            }
            finally
            {
                await newApiApp.StopAsync();
                await newApiApp.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Helper class acting as a mock initiator application for testing
    /// </summary>
    internal class TestInitiatorApplication : IApplication
    {
        public SessionID? Session { get; private set; }
        private readonly TaskCompletionSource _logonTcs = new();

        public async Task WaitForLogonAsync(TimeSpan timeout)
        {
            var completedTask = await Task.WhenAny(_logonTcs.Task, Task.Delay(timeout));
            if (completedTask != _logonTcs.Task)
            {
                throw new TimeoutException("FIX Initiator connection/logon timed out.");
            }
        }

        public void OnCreate(SessionID sessionID) { }

        public void OnLogon(SessionID sessionID)
        {
            Session = sessionID;
            _logonTcs.TrySetResult();
            Console.WriteLine($"[TEST-INITIATOR] Logon completed for session {sessionID}");
        }

        public void OnLogout(SessionID sessionID)
        {
            Session = null;
            Console.WriteLine($"[TEST-INITIATOR] Logout completed for session {sessionID}");
        }

        public void FromAdmin(QuickFix.Message message, SessionID sessionID) { }
        public void FromApp(QuickFix.Message message, SessionID sessionID)
        {
            Console.WriteLine($"[TEST-INITIATOR] Received App msg: {message}");
        }

        public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }
        public void ToApp(QuickFix.Message message, SessionID sessionID) { }
    }
}
