using OrderGenerator.Infrastructure.FIX;
using OrderGenerator.WebAPI.Hubs;

namespace OrderGenerator.WebAPI.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseFIXInitiator(this IApplicationBuilder app)
        {
            var fixInitiator = app.ApplicationServices.GetRequiredService<IFIXSessionManager>();

            try
            {
                string fixConfigPath = Path.Combine(AppContext.BaseDirectory, "fix.cfg");
                
                if (!File.Exists(fixConfigPath))
                {
                    throw new FileNotFoundException($"FIX configuration not found at {fixConfigPath}");
                }
                
                fixInitiator.Start(fixConfigPath);
                Console.WriteLine($"[BACKEND] FIX Initiator started with config: {fixConfigPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKEND-ERROR] Failed to start FIX Initiator: {ex.Message}");
            }

            return app;
        }

        public static WebApplication ConfigurePipeline(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<OrderHub>("/orderhub");

            return app;
        }
    }
}
