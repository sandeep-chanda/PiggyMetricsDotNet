using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;
using Testcontainers.MongoDb;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Repository;

public class UserRepositoryTest : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").Build();

    private IMongoDatabase _database = null!;
    private MongoUserRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        _database = client.GetDatabase("piggymetrics");
        _repository = new MongoUserRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public void shouldSaveAndFindUserByName()
    {
        var user = new User
        {
            Username = "name",
            Password = "password"
        };
        _repository.Save(user);

        var found = _repository.FindById(user.Username!);
        Assert.NotNull(found);
        Assert.Equal(user.Username, found!.Username);
        Assert.Equal(user.Password, found.Password);

        var stored = _database.GetCollection<BsonDocument>("users")
            .Find(new BsonDocument("_id", user.Username))
            .First();
        Assert.Equal("name", stored["_id"].AsString);
        Assert.Equal("name", stored["username"].AsString);
        Assert.Equal("password", stored["password"].AsString);
    }
}
