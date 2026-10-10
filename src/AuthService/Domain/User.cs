using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace PiggyMetrics.AuthService.Domain;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [JsonPropertyName("username")]
    [Required]
    [MinLength(3)]
    [MaxLength(20)]
    public string? Username { get; set; }

    [BsonElement("password")]
    [JsonPropertyName("password")]
    [Required]
    [MinLength(6)]
    [MaxLength(40)]
    public string? Password { get; set; }
}
