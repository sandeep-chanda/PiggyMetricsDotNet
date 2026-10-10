using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

public sealed class Saving
{
    [JsonPropertyName("amount")]
    [Required]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    [Required]
    public Currency? Currency { get; set; }

    [JsonPropertyName("interest")]
    [Required]
    public decimal? Interest { get; set; }

    [JsonPropertyName("deposit")]
    [Required]
    public bool? Deposit { get; set; }

    [JsonPropertyName("capitalization")]
    [Required]
    public bool? Capitalization { get; set; }
}
