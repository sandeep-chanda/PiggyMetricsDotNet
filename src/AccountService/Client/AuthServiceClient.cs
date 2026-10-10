using PiggyMetrics.AccountService.Domain;

namespace PiggyMetrics.AccountService.Client;

public interface AuthServiceClient
{
    void CreateUser(User user);
}
