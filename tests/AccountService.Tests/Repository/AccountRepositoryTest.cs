using MongoDB.Driver;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.AccountService.Repository;
using Testcontainers.MongoDb;
using Xunit;

namespace PiggyMetrics.AccountService.Tests.Repository;

public class AccountRepositoryTest : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").Build();

    private MongoAccountRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        var database = client.GetDatabase("piggymetrics");
        _repository = new MongoAccountRepository(database);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public void shouldFindAccountByName()
    {
        var stub = GetStubAccount();
        _repository.Save(stub);

        var found = _repository.FindByName(stub.Name!);

        Assert.NotNull(found);
        Assert.Equal(stub.LastSeen, found.LastSeen);
        Assert.Equal(stub.Note, found.Note);
        Assert.Equal(stub.Incomes!.Count, found.Incomes!.Count);
        Assert.Equal(stub.Expenses!.Count, found.Expenses!.Count);
    }

    private static Account GetStubAccount()
    {
        var saving = new Saving
        {
            Amount = new decimal(1500),
            Currency = Currency.USD,
            Interest = new decimal(3.32),
            Deposit = true,
            Capitalization = false
        };
        var vacation = new Item
        {
            Title = "Vacation",
            Amount = new decimal(3400),
            Currency = Currency.EUR,
            Period = TimePeriod.YEAR,
            Icon = "tourism"
        };
        var grocery = new Item
        {
            Title = "Grocery",
            Amount = new decimal(10),
            Currency = Currency.USD,
            Period = TimePeriod.DAY,
            Icon = "meal"
        };
        var salary = new Item
        {
            Title = "Salary",
            Amount = new decimal(9100),
            Currency = Currency.USD,
            Period = TimePeriod.MONTH,
            Icon = "wallet"
        };

        return new Account
        {
            Name = "test",
            Note = "test note",
            LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            Saving = saving,
            Expenses = new List<Item> { grocery, vacation },
            Incomes = new List<Item> { salary }
        };
    }
}
