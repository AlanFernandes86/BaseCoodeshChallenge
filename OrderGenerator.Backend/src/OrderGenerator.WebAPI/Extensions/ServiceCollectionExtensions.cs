using OrderGenerator.Application.Services;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Domain.Services;
using OrderGenerator.Infrastructure.FIX;

namespace OrderGenerator.WebAPI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFIXServices(this IServiceCollection services)
        {
            services.AddSingleton<FIXInitiatorService>();
            services.AddSingleton<IOrderSubmissionService>(sp => sp.GetRequiredService<FIXInitiatorService>());
            services.AddSingleton<IFIXSessionManager>(sp => sp.GetRequiredService<FIXInitiatorService>());
            
            services.AddSingleton<IOrderTracker, OrderTracker>();
            services.AddSingleton<SubmitOrderUseCase>();
            services.AddHostedService<HandleExecutionReportService>();            
            services.AddHostedService<OrderOutboxService>();

            return services;
        }

        public static IServiceCollection AddWebApiServices(this IServiceCollection services)
        {
            services.AddSignalR();
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
            services.AddControllers();

            return services;
        }
    }
}
