using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.AuthService.Repository;

public sealed class MongoUserRepository : UserRepository
{
    private readonly IMongoCollection<BsonDocument> _documents;

    public MongoUserRepository(IMongoDatabase database)
    {
        _documents = database.GetCollection<BsonDocument>(MongoCollectionNames.Users);
    }

    public User? FindById(string id)
    {
        var document = _documents.Find(new BsonDocument("_id", id)).FirstOrDefault();
        if (document is null)
        {
            return null;
        }

        var username = document.Contains("_id") && document["_id"].IsString
            ? document["_id"].AsString
            : id;

        string? password = null;
        if (document.Contains("password") && document["password"].IsString)
        {
            password = document["password"].AsString;
        }

        return new User
        {
            Username = username,
            Password = password
        };
    }

    public void Save(User user)
    {
        var username = user.Username ?? throw new ArgumentException("username required");
        var document = new BsonDocument
        {
            ["_id"] = username,
            ["password"] = user.Password is null ? BsonNull.Value : user.Password
        };
        _documents.ReplaceOne(
            new BsonDocument("_id", username),
            document,
            new ReplaceOptions { IsUpsert = true });
    }
}
