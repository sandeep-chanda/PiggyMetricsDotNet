using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.NotificationService.Domain;

public sealed class NotificationSettings
{
    [JsonPropertyName("active")]
    [Required]
    public bool? Active { get; set; }

    [JsonPropertyName("frequency")]
    [Required]
    public Frequency? Frequency { get; set; }

    [JsonPropertyName("lastNotified")]
    public DateTimeOffset? LastNotified { get; set; }
}
