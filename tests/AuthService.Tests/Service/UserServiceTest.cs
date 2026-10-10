using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;
using PiggyMetrics.AuthService.Service;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Service;

public class UserServiceTest
{
    private readonly UserServiceImpl _userService;
    private readonly Mock<UserRepository> _repository = new();

    public UserServiceTest()
    {
        _userService = new UserServiceImpl(_repository.Object, NullLogger<UserServiceImpl>.Instance);
    }

    [Fact]
    public void shouldCreateUser()
    {
        var user = new User
        {
            Username = "name",
            Password = "password"
        };

        _userService.Create(user);

        _repository.Verify(repository => repository.Save(user), Times.Once);
    }

    [Fact]
    public void shouldFailWhenUserAlreadyExists()
    {
        var user = new User
        {
            Username = "name",
            Password = "password"
        };
        _repository.Setup(repository => repository.FindById(user.Username!)).Returns(new User());

        Assert.Throws<ArgumentException>(() => _userService.Create(user));
    }
}
