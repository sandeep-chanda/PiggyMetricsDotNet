using Moq;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;
using PiggyMetrics.AuthService.Service.Security;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Service.Security;

public class MongoUserDetailsServiceTest
{
    private readonly MongoUserDetailsService _service;
    private readonly Mock<UserRepository> _repository = new();

    public MongoUserDetailsServiceTest()
    {
        _service = new MongoUserDetailsService(_repository.Object);
    }

    [Fact]
    public void shouldLoadByUsernameWhenUserExists()
    {
        var user = new User();
        _repository.Setup(repository => repository.FindById(It.IsAny<string>())).Returns(user);

        var loaded = _service.LoadUserByUsername("name");

        Assert.Equal(user, loaded);
    }

    [Fact]
    public void shouldFailToLoadByUsernameWhenUserNotExists()
    {
        Assert.Throws<UsernameNotFoundException>(() => _service.LoadUserByUsername("name"));
    }
}
