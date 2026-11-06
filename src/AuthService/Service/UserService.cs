using PiggyMetrics.AuthService.Domain;

namespace PiggyMetrics.AuthService.Service;

public interface UserService
{
    void Create(User user);
}
