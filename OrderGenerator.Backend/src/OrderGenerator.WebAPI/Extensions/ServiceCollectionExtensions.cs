using OrderGenerator.Application.UseCases;
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
            var fixInitiator = new FIXInitiatorService();
            services.AddSingleton<IFIXOrderSubmissionService>(fixInitiator);
            services.AddSingleton(fixInitiator);
            services.AddSingleton(new OrderSubmissionService(fixInitiator));
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
