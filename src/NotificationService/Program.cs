using PiggyMetrics.Shared;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("notification-service");
builder.Services.AddPiggyMetricsResourceServer(builder.Configuration);

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
