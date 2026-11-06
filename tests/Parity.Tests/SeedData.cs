extern alias AccountApp;
extern alias NotificationApp;
extern alias StatisticsApp;

using System.Text.Json;
using AccountItem = AccountApp::PiggyMetrics.AccountService.Domain.Item;
using AccountTimePeriod = AccountApp::PiggyMetrics.AccountService.Domain.TimePeriod;
using PiggyMetrics.Shared.Hosting;
using Account = AccountApp::PiggyMetrics.AccountService.Domain.Account;
using AccountCurrency = AccountApp::PiggyMetrics.AccountService.Domain.Currency;
using Saving = AccountApp::PiggyMetrics.AccountService.Domain.Saving;
using DataPoint = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.DataPoint;
using DataPointId = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.DataPointId;
using ItemMetric = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.ItemMetric;
using StatisticMetric = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.StatisticMetric;
using StatisticsCurrency = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Currency;
using Frequency = NotificationApp::PiggyMetrics.NotificationService.Domain.Frequency;
using NotificationSettings = NotificationApp::PiggyMetrics.NotificationService.Domain.NotificationSettings;
using NotificationType = NotificationApp::PiggyMetrics.NotificationService.Domain.NotificationType;
using Recipient = NotificationApp::PiggyMetrics.NotificationService.Domain.Recipient;

namespace PiggyMetrics.Parity.Tests;

public static class SeedData
{
    public static readonly DateTimeOffset DemoLastSeen = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
    public static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    public static JsonSerializerOptions Json { get; } = PiggyMetricsJsonOptions.Create();

    public static Account DemoAccount() => new()
    {
        Name = "demo",
        Note = "demo note",
        LastSeen = DemoLastSeen,
        Saving = new Saving
        {
            Amount = 5900m,
            Capitalization = false,
            Currency = AccountCurrency.USD,
            Deposit = true,
            Interest = 3.32m
        },
        Expenses = new List<AccountItem>
        {
            Item("Rent", 1300m, AccountCurrency.USD, AccountTimePeriod.MONTH, "home"),
            Item("Utilities", 120m, AccountCurrency.USD, AccountTimePeriod.MONTH, "utilities"),
            Item("Meal", 20m, AccountCurrency.USD, AccountTimePeriod.DAY, "meal"),
            Item("Gas", 240m, AccountCurrency.USD, AccountTimePeriod.MONTH, "gas"),
            Item("Vacation", 3500m, AccountCurrency.EUR, AccountTimePeriod.YEAR, "island"),
            Item("Phone", 30m, AccountCurrency.EUR, AccountTimePeriod.MONTH, "phone"),
            Item("Gym", 700m, AccountCurrency.USD, AccountTimePeriod.YEAR, "sport")
        },
        Incomes = new List<AccountItem>
        {
            Item("Salary", 42000m, AccountCurrency.USD, AccountTimePeriod.YEAR, "wallet"),
            Item("Scholarship", 500m, AccountCurrency.USD, AccountTimePeriod.MONTH, "edu")
        }
    };

    public static Account NamedAccount(string name, string note) => new()
    {
        Name = name,
        Note = note,
        Saving = EmptySaving()
    };

    public static Saving EmptySaving() => new()
    {
        Amount = 0m,
        Currency = AccountCurrency.USD,
        Interest = 0m,
        Deposit = false,
        Capitalization = false
    };

    public static Account CreatedAccount(string username) => new()
    {
        Name = username,
        LastSeen = DateTimeOffset.UtcNow,
        Saving = EmptySaving()
    };

    public static DataPoint DemoDataPoint() => new()
    {
        Id = new DataPointId("demo", DateTime.UnixEpoch),
        Incomes = new HashSet<ItemMetric> { new("salary", 1m) },
        Expenses = new HashSet<ItemMetric> { new("grocery", 2m) },
        Statistics = new Dictionary<StatisticMetric, decimal>
        {
            [StatisticMetric.SAVING_AMOUNT] = 3m
        },
        Rates = new Dictionary<StatisticsCurrency, decimal>
        {
            [StatisticsCurrency.USD] = 1m
        }
    };

    public static Recipient DemoRecipient() => new()
    {
        AccountName = "demo",
        Email = "demo@piggymetrics.test",
        ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
        {
            [NotificationType.BACKUP] = new()
            {
                Active = false,
                Frequency = Frequency.MONTHLY,
                LastNotified = Epoch
            },
            [NotificationType.REMIND] = new()
            {
                Active = true,
                Frequency = Frequency.WEEKLY,
                LastNotified = Epoch
            }
        }
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static AccountItem Item(string title, decimal amount, AccountCurrency currency, AccountTimePeriod period, string icon) => new()
    {
        Title = title,
        Amount = amount,
        Currency = currency,
        Period = period,
        Icon = icon
    };
}
