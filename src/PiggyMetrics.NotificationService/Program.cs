using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddPiggyServiceDefaults(new PiggyServiceOptions
{
    ServiceName = "notification-service",
    PathBase = "/notifications",
});

builder.Services.AddPiggyBearerAuthentication(builder.Configuration);

var app = builder.Build();

app.UsePiggyServiceDefaults();

app.Run();
