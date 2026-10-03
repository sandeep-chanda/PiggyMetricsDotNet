using PiggyMetrics.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddPiggyServiceDefaults(new PiggyServiceOptions
{
    ServiceName = "auth-service",
    PathBase = "/uaa",
});

var app = builder.Build();

app.UsePiggyServiceDefaults();

app.Run();
