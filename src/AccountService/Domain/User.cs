using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.AccountService.Domain;

public sealed class User
{
    [JsonPropertyName("username")]
    [Required]
    [MinLength(3)]
    [MaxLength(20)]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    [Required]
    [MinLength(6)]
    [MaxLength(40)]
    public string? Password { get; set; }
}
