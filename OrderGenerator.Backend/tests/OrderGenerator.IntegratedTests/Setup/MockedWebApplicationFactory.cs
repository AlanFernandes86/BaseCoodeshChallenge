using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrderGenerator.Domain.Services;
using OrderGenerator.Infrastructure.FIX;

namespace OrderGenerator.IntegratedTests.Setup
{
    public class MockedWebApplicationFactory : WebApplicationFactory<Program>
    {
        public Mock<IOrderSubmissionService> MockFixService { get; } = new();
        public Mock<IFIXSessionManager> MockSessionManager { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real FIX services
                services.RemoveAll<IOrderSubmissionService>();
                services.RemoveAll<IFIXSessionManager>();

                // Inject mocks
                services.AddSingleton(MockFixService.Object);
                services.AddSingleton(MockSessionManager.Object);
            });
        }
    }
}
