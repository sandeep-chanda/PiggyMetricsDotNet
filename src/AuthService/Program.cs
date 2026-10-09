using PiggyMetrics.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("auth-service");

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
