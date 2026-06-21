using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderGenerator.Domain.Services;
using OrderGenerator.WebAPI.Hubs;

namespace OrderGenerator.Application.Services
{
    public class HandleExecutionReportService : BackgroundService
    {
        private readonly IOrderSubmissionService _orderSubmissionService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ILogger<HandleExecutionReportService> _logger;

        public HandleExecutionReportService(
            IOrderSubmissionService orderSubmissionService,
            IHubContext<OrderHub> hubContext,
            ILogger<HandleExecutionReportService> logger)
        {
            _orderSubmissionService = orderSubmissionService ?? throw new ArgumentNullException(nameof(orderSubmissionService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[HANDLE-EXEC-REPORT] Starting execution report listener...");

            _orderSubmissionService.ExecutionReportReceived += OnExecutionReportReceived;

            // Wait until cancellation is requested
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            stoppingToken.Register(() => tcs.TrySetResult());

            return tcs.Task;
        }

        private async void OnExecutionReportReceived(object? sender, ExecutionReportData report)
        {
            try
            {
                _logger.LogInformation($"[HANDLE-EXEC-REPORT] Broadcasting execution report for ClOrdID {report.ClOrdID}");
                
                await _hubContext.Clients.All.SendAsync("OrderResult", new
                {
                    success = report.IsAccepted,
                    status = report.OrdStatus == "8" ? "REJECTED" : report.IsAccepted ? "ACCEPTED" : report.OrdStatus,
                    orderId = report.OrderID,
                    clOrdId = report.ClOrdID,
                    message = report.Text,
                    timestamp = report.ReceivedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[HANDLE-EXEC-REPORT] Error sending to SignalR: {ex.Message}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[HANDLE-EXEC-REPORT] Stopping execution report listener...");
            _orderSubmissionService.ExecutionReportReceived -= OnExecutionReportReceived;
            await base.StopAsync(cancellationToken);
        }
    }
}
