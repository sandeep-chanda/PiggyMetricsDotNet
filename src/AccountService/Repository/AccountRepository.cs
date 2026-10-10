using PiggyMetrics.AccountService.Domain;

namespace PiggyMetrics.AccountService.Repository;

public interface AccountRepository
{
    Account? FindByName(string name);

    void Save(Account account);
}
