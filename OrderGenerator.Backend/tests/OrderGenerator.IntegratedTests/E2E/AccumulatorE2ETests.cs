using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using OrderGenerator.WebAPI.Models;

namespace OrderGenerator.IntegratedTests.E2E
{
    /// <summary>
    /// WARNING: This test acts as a real external client.
    /// It requires BOTH the OrderAccumulator AND the OrderGenerator.Backend to be running via Docker (or locally).
    /// The Backend must be exposed on http://localhost:5000.
    /// </summary>
    public class AccumulatorE2ETests
    {
        private const string BackendUrl = "http://localhost:5000";

        [Fact(Skip = "Requires real OrderAccumulator running via Docker - Remova o Skip para rodar o teste")]
        public async Task SubmitOrder_ViaSignalR_AgainstRealDockerBackend_ReceivesAcceptedExecutionReport()
        {
            // Arrange
            var httpClient = new HttpClient { BaseAddress = new Uri(BackendUrl) };
            
            // Check if backend is actually running, if not, skip/fail early
            try
            {
                await httpClient.GetAsync("/swagger/index.html"); // Just a ping to see if it's up
            }
            catch (HttpRequestException)
            {
                Assert.Fail("The Backend is not running at http://localhost:5000. Please run docker-compose up before executing this E2E test.");
            }

            // Create a real HubConnection over the network
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{BackendUrl}/orderhub")
                .Build();

            var tcs = new TaskCompletionSource<SubmitOrderResponse>();

            hubConnection.On<SubmitOrderResponse>("OrderResult", response =>
            {
                // We might receive the PENDING first, and then the ACCEPTED/REJECTED.
                // We want to wait for the final execution report.
                if (response.Status != "PENDING" && response.Status != "ERROR")
                {
                    tcs.TrySetResult(response);
                }
            });

            await hubConnection.StartAsync();

            // Act
            // Instead of invoking hub method (which also works), we can test the HTTP REST endpoint 
            // and listen to the SignalR broadcast (or use SignalR directly). Let's use SignalR directly as before.
            await hubConnection.InvokeAsync("SubmitOrder", "VALE3", "Sell", 200m, 60.50m);

            // Assert
            // Wait for the ExecutionReport to come back from the real Accumulator (timeout 10 seconds)
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            
            if (completedTask != tcs.Task)
            {
                Assert.Fail("Timed out waiting for ExecutionReport from OrderAccumulator. Ensure both Backend and Accumulator are running and connected in Docker.");
            }

            var finalResult = await tcs.Task;
            finalResult.Should().NotBeNull();
            finalResult.Success.Should().BeTrue();
            finalResult.Status.Should().Be("ACCEPTED");
            
            await hubConnection.StopAsync();
        }
    }
}
