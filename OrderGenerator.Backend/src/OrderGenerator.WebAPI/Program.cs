using OrderGenerator.Infrastructure.FIX;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Domain.Services;
using OrderGenerator.WebAPI.Hubs;
using OrderGenerator.WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add FIX services
var fixInitiator = new FIXInitiatorService();
builder.Services.AddSingleton<IFIXOrderSubmissionService>(fixInitiator);
builder.Services.AddSingleton(fixInitiator);
builder.Services.AddSingleton(new OrderSubmissionService(fixInitiator as IFIXOrderSubmissionService ?? throw new InvalidOperationException()));
builder.Services.AddHostedService<FIXOutboxWorker>();

// Add services to the container
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
builder.Services.AddControllers();

var app = builder.Build();

// Initialize FIX Initiator
try
{
    // Load FIX configuration from bin directory (copied during build)
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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHub<OrderHub>("/orderhub");

app.Run();
