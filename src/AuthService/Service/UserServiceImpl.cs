using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;

namespace PiggyMetrics.AuthService.Service;

public sealed class UserServiceImpl : UserService
{
    private readonly UserRepository _repository;
    private readonly ILogger<UserServiceImpl> _logger;

    public UserServiceImpl(UserRepository repository, ILogger<UserServiceImpl> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void Create(User user)
    {
        var existing = _repository.FindById(user.Username ?? string.Empty);
        if (existing is not null)
        {
            throw new ArgumentException("user already exists: " + existing.Username);
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        _repository.Save(user);
        _logger.LogInformation("new user has been created: {Username}", user.Username);
    }
}
