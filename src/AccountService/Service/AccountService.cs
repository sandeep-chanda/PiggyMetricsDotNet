using PiggyMetrics.AccountService.Domain;

namespace PiggyMetrics.AccountService.Service;

public interface AccountService
{
    Account? FindByName(string accountName);

    Account Create(User user);

    void SaveChanges(string name, Account update);
}
