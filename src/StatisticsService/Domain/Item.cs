using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

public sealed class Item
{
    [JsonPropertyName("title")]
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string? Title { get; set; }

    [JsonPropertyName("amount")]
    [Required]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    [Required]
    public Currency? Currency { get; set; }

    [JsonPropertyName("period")]
    [Required]
    public TimePeriod? Period { get; set; }
}
