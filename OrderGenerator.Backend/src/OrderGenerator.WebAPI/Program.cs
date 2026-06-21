using Microsoft.AspNetCore.Builder;
using OrderGenerator.WebAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices();
builder.Services.AddWebApiServices();

var app = builder.Build();

app.UseFIXInitiator();
app.ConfigurePipeline();

app.Run();
