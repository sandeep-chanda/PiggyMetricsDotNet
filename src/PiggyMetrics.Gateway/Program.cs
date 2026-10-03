using PiggyMetrics.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddPiggyServiceDefaults(new PiggyServiceOptions
{
    ServiceName = "gateway",
    PathBase = string.Empty,
});

var app = builder.Build();

app.UsePiggyServiceDefaults();

app.Run();
