using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Repository;

public class UserRepositoryTest : IDisposable
{
    private readonly MongoDbRunner _runner;
    private readonly IMongoDatabase _database;
    private readonly MongoUserRepository _repository;

    public UserRepositoryTest()
    {
        _runner = MongoDbRunner.Start();
        var client = new MongoClient(_runner.ConnectionString);
        _database = client.GetDatabase("piggymetrics");
        _repository = new MongoUserRepository(_database);
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

    public void Dispose()
    {
        _runner.Dispose();
    }
}
