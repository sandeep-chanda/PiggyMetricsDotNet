using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddPiggyServiceDefaults(new PiggyServiceOptions
{
    ServiceName = "account-service",
    PathBase = "/accounts",
});

builder.Services.AddPiggyBearerAuthentication(builder.Configuration);

var app = builder.Build();

app.UsePiggyServiceDefaults();

app.Run();
