using PiggyMetrics.AuthService.Domain;

namespace PiggyMetrics.AuthService.Repository;

public interface UserRepository
{
    User? FindById(string id);

    void Save(User user);
}
