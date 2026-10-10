using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;

namespace PiggyMetrics.AuthService.Service.Security;

public sealed class MongoUserDetailsService
{
    private readonly UserRepository _repository;

    public MongoUserDetailsService(UserRepository repository)
    {
        _repository = repository;
    }

    public User LoadUserByUsername(string username)
    {
        return _repository.FindById(username) ?? throw new UsernameNotFoundException(username);
    }
}
