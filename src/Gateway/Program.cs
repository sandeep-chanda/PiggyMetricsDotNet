using PiggyMetrics.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsSharedDefaults();
var app = builder.Build();
app.MapPiggyMetricsHealthChecks();
app.Run();

public partial class Program { }
