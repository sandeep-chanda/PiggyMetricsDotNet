using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.AccountService.Repository;

namespace PiggyMetrics.AccountService.Service;

public sealed class AccountServiceImpl : AccountService
{
    private readonly AccountRepository _repository;
    private readonly AuthServiceClient _authClient;
    private readonly StatisticsServiceClient _statisticsClient;
    private readonly ILogger<AccountServiceImpl> _logger;

    public AccountServiceImpl(
        AccountRepository repository,
        AuthServiceClient authClient,
        StatisticsServiceClient statisticsClient,
        ILogger<AccountServiceImpl> logger)
    {
        _repository = repository;
        _authClient = authClient;
        _statisticsClient = statisticsClient;
        _logger = logger;
    }

    public Account? FindByName(string accountName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("this String argument must have length; it must not be null or empty");
        }

        return _repository.FindByName(accountName);
    }

    public Account Create(User user)
    {
        var existing = _repository.FindByName(user.Username ?? string.Empty);
        if (existing is not null)
        {
            throw new ArgumentException("account already exists: " + user.Username);
        }

        _authClient.CreateUser(user);

        var saving = new Saving
        {
            Amount = new decimal(0),
            Currency = CurrencyCodes.GetDefault(),
            Interest = new decimal(0),
            Deposit = false,
            Capitalization = false
        };

        var account = new Account
        {
            Name = user.Username,
            LastSeen = DateTimeOffset.UtcNow,
            Saving = saving
        };

        _repository.Save(account);
        _logger.LogInformation("new account has been created: {Name}", account.Name);
        return account;
    }

    public void SaveChanges(string name, Account update)
    {
        var account = _repository.FindByName(name);
        if (account is null)
        {
            throw new ArgumentException("can't find account with name " + name);
        }

        account.Incomes = update.Incomes;
        account.Expenses = update.Expenses;
        account.Saving = update.Saving;
        account.Note = update.Note;
        account.LastSeen = DateTimeOffset.UtcNow;
        _repository.Save(account);
        _logger.LogDebug("account {Name} changes has been saved", name);
        _statisticsClient.UpdateStatistics(name, account);
    }
}
