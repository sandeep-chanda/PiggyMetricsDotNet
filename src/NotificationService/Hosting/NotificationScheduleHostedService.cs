using PiggyMetrics.NotificationService.Config;
using NotificationServiceApi = PiggyMetrics.NotificationService.Service.NotificationService;

namespace PiggyMetrics.NotificationService.Hosting;

public sealed class NotificationScheduleHostedService : BackgroundService
{
    private readonly NotificationServiceApi _notifications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationScheduleHostedService> _logger;

    public NotificationScheduleHostedService(
        NotificationServiceApi notifications,
        IConfiguration configuration,
        ILogger<NotificationScheduleHostedService> logger)
    {
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            RunAsync("backup:cron", "0 0 12 * * *", service => service.SendBackupNotifications(), stoppingToken),
            RunAsync("remind:cron", "0 0 0 * * *", service => service.SendRemindNotifications(), stoppingToken));
    }

    private async Task RunAsync(
        string configKey,
        string fallbackCron,
        Action<NotificationServiceApi> invoke,
        CancellationToken stoppingToken)
    {
        var cron = _configuration[configKey];
        if (string.IsNullOrWhiteSpace(cron))
        {
            cron = fallbackCron;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = SpringCron.DelayUntilNext(cron, DateTimeOffset.UtcNow);
                await Task.Delay(delay, stoppingToken);
                invoke(_notifications);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "notification schedule {ConfigKey} failed", configKey);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
