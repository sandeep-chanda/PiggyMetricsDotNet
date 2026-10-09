using PiggyMetrics.Shared.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsHealthChecks();

var app = builder.Build();
app.UseRouting();
app.MapPiggyMetricsHealthChecks();
app.Run();
