using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

public sealed class Account
{
    [JsonPropertyName("incomes")]
    [Required]
    public List<Item>? Incomes { get; set; }

    [JsonPropertyName("expenses")]
    [Required]
    public List<Item>? Expenses { get; set; }

    [JsonPropertyName("saving")]
    [Required]
    public Saving? Saving { get; set; }
}
