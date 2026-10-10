using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.AccountService.Repository;
using PiggyMetrics.Shared.Store;
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
        Assert.Equal(stub.Name, found.Name);
        Assert.Equal(stub.Saving!.Amount, found.Saving!.Amount);
        Assert.Equal(stub.Saving.Currency, found.Saving.Currency);
        Assert.Equal(stub.Saving.Interest, found.Saving.Interest);
        Assert.Equal(stub.Saving.Deposit, found.Saving.Deposit);
        Assert.Equal(stub.Saving.Capitalization, found.Saving.Capitalization);
        Assert.Equal("Grocery", found.Expenses[0].Title);
        Assert.Equal(10m, found.Expenses[0].Amount);
        Assert.Equal(Currency.USD, found.Expenses[0].Currency);
        Assert.Equal(TimePeriod.DAY, found.Expenses[0].Period);
        Assert.Equal("meal", found.Expenses[0].Icon);
        Assert.Equal("Salary", found.Incomes[0].Title);
        Assert.Equal(9100m, found.Incomes[0].Amount);
        Assert.Equal(Currency.USD, found.Incomes[0].Currency);
        Assert.Equal(TimePeriod.MONTH, found.Incomes[0].Period);
        Assert.Equal("wallet", found.Incomes[0].Icon);

        var database = new MongoClient(_container.GetConnectionString()).GetDatabase(MongoCollectionNames.Database);
        var raw = database.GetCollection<BsonDocument>(MongoCollectionNames.Accounts)
            .Find(new BsonDocument("_id", "test"))
            .First();
        Assert.Equal("accounts", MongoCollectionNames.Accounts);
        Assert.Equal("piggymetrics", MongoCollectionNames.Database);
        Assert.Equal("test", raw["_id"].AsString);
        Assert.False(raw.Contains("name"));
        Assert.Equal("test note", raw["note"].AsString);
        Assert.Equal("USD", raw["saving"].AsBsonDocument["currency"].AsString);
        Assert.Equal(3.32m, raw["saving"].AsBsonDocument["interest"].AsDecimal);
        Assert.Equal("Vacation", raw["expenses"].AsBsonArray[1].AsBsonDocument["title"].AsString);
        Assert.Equal("EUR", raw["expenses"].AsBsonArray[1].AsBsonDocument["currency"].AsString);
    }

    private static Account GetStubAccount()
    {
        var saving = new Saving
        {
            Amount = new decimal(1500),
            Currency = Currency.USD,
            Interest = 3.32m,
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
