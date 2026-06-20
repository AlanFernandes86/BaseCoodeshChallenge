using OrderGenerator.Infrastructure.FIX;
using OrderGenerator.Application.UseCases;
using OrderGenerator.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add FIX services
var fixInitiator = new FIXInitiatorService();
builder.Services.AddSingleton<IFIXOrderSubmissionService>(fixInitiator);
builder.Services.AddSingleton(fixInitiator);
builder.Services.AddSingleton(new OrderSubmissionService(fixInitiator as IFIXOrderSubmissionService ?? throw new InvalidOperationException()));

// Add services to the container
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost", "http://frontend", "http://frontend:5173")
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
    string fixConfigPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "fix.cfg");
    if (!File.Exists(fixConfigPath))
    {
        fixConfigPath = "/app/fix.cfg";  // Docker path
    }
    
    fixInitiator.Start(fixConfigPath);
    Console.WriteLine("[BACKEND] FIX Initiator started");
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
