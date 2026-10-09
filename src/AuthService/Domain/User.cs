using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace PiggyMetrics.AuthService.Domain;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [BsonElement("password")]
    [JsonPropertyName("password")]
    public string? Password { get; set; }
}
