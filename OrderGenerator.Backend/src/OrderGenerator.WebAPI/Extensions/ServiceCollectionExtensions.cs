using OrderGenerator.Application.Services;
using OrderGenerator.Domain.Services;
using OrderGenerator.Infrastructure.FIX;
using OrderGenerator.WebAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace OrderGenerator.WebAPI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFIXServices(this IServiceCollection services)
        {
            services.AddSingleton<IFIXOrderSubmissionService, FIXInitiatorService>();
            services.AddSingleton<OrderSubmissionService>();
            services.AddHostedService<FIXOutboxWorker>();

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
