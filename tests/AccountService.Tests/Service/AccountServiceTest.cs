using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.AccountService.Repository;
using PiggyMetrics.AccountService.Service;
using Xunit;

namespace PiggyMetrics.AccountService.Tests.Service;

public class AccountServiceTest
{
    private readonly AccountServiceImpl _accountService;
    private readonly Mock<StatisticsServiceClient> _statisticsClient = new();
    private readonly Mock<AuthServiceClient> _authClient = new();
    private readonly Mock<AccountRepository> _repository = new();

    public AccountServiceTest()
    {
        _accountService = new AccountServiceImpl(
            _repository.Object,
            _authClient.Object,
            _statisticsClient.Object,
            NullLogger<AccountServiceImpl>.Instance);
    }

    [Fact]
    public void shouldFindByName()
    {
        var account = new Account
        {
            Name = "test"
        };
        _repository.Setup(repository => repository.FindByName(account.Name!)).Returns(account);

        var found = _accountService.FindByName(account.Name!);

        Assert.Equal(account, found);
    }

    [Fact]
    public void shouldFailWhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _accountService.FindByName(""));
    }

    [Fact]
    public void shouldCreateAccountWithGivenUser()
    {
        var user = new User
        {
            Username = "test"
        };

        var account = _accountService.Create(user);

        Assert.Equal(user.Username, account.Name);
        Assert.Equal(0, decimal.ToInt32(account.Saving!.Amount!.Value));
        Assert.Equal(CurrencyCodes.GetDefault(), account.Saving.Currency);
        Assert.Equal(0, decimal.ToInt32(account.Saving.Interest!.Value));
        Assert.Equal(false, account.Saving.Deposit);
        Assert.Equal(false, account.Saving.Capitalization);
        Assert.NotNull(account.LastSeen);

        _authClient.Verify(client => client.CreateUser(user), Times.Once);
        _repository.Verify(repository => repository.Save(account), Times.Once);
    }

    [Fact]
    public void shouldSaveChangesWhenUpdatedAccountGiven()
    {
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
        var saving = new Saving
        {
            Amount = new decimal(1500),
            Currency = Currency.USD,
            Interest = new decimal(3.32),
            Deposit = true,
            Capitalization = false
        };
        var update = new Account
        {
            Name = "test",
            Note = "test note",
            Incomes = new List<Item> { salary },
            Expenses = new List<Item> { grocery },
            Saving = saving
        };
        var account = new Account();
        _repository.Setup(repository => repository.FindByName("test")).Returns(account);

        _accountService.SaveChanges("test", update);

        Assert.Equal(update.Note, account.Note);
        Assert.NotNull(account.LastSeen);
        Assert.Equal(update.Saving.Amount, account.Saving!.Amount);
        Assert.Equal(update.Saving.Currency, account.Saving.Currency);
        Assert.Equal(update.Saving.Interest, account.Saving.Interest);
        Assert.Equal(update.Saving.Deposit, account.Saving.Deposit);
        Assert.Equal(update.Saving.Capitalization, account.Saving.Capitalization);
        Assert.Equal(update.Expenses!.Count, account.Expenses!.Count);
        Assert.Equal(update.Incomes!.Count, account.Incomes!.Count);
        Assert.Equal(update.Expenses[0].Title, account.Expenses[0].Title);
        Assert.Equal(0, decimal.Compare(update.Expenses[0].Amount!.Value, account.Expenses[0].Amount!.Value));
        Assert.Equal(update.Expenses[0].Currency, account.Expenses[0].Currency);
        Assert.Equal(update.Expenses[0].Period, account.Expenses[0].Period);
        Assert.Equal(update.Expenses[0].Icon, account.Expenses[0].Icon);
        Assert.Equal(update.Incomes[0].Title, account.Incomes[0].Title);
        Assert.Equal(0, decimal.Compare(update.Incomes[0].Amount!.Value, account.Incomes[0].Amount!.Value));
        Assert.Equal(update.Incomes[0].Currency, account.Incomes[0].Currency);
        Assert.Equal(update.Incomes[0].Period, account.Incomes[0].Period);
        Assert.Equal(update.Incomes[0].Icon, account.Incomes[0].Icon);

        _repository.Verify(repository => repository.Save(account), Times.Once);
        _statisticsClient.Verify(client => client.UpdateStatistics("test", account), Times.Once);
    }

    [Fact]
    public void shouldFailWhenNoAccountsExistedWithGivenName()
    {
        var update = new Account
        {
            Incomes = new List<Item> { new() },
            Expenses = new List<Item> { new() }
        };
        _repository.Setup(repository => repository.FindByName("test")).Returns((Account?)null);

        Assert.Throws<ArgumentException>(() => _accountService.SaveChanges("test", update));
    }
}
