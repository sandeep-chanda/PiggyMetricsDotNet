using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.AccountService.Domain;

public sealed class Account
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("lastSeen")]
    public DateTimeOffset? LastSeen { get; set; }

    [JsonPropertyName("incomes")]
    public List<Item>? Incomes { get; set; }

    [JsonPropertyName("expenses")]
    public List<Item>? Expenses { get; set; }

    [JsonPropertyName("saving")]
    [Required]
    public Saving? Saving { get; set; }

    [JsonPropertyName("note")]
    [MaxLength(20_000)]
    public string? Note { get; set; }
}
