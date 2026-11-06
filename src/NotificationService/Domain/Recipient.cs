using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PiggyMetrics.NotificationService.Domain;

public sealed class Recipient : IValidatableObject
{
    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("email")]
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [JsonPropertyName("scheduledNotifications")]
    public Dictionary<NotificationType, NotificationSettings>? ScheduledNotifications { get; set; }

    public override string ToString()
    {
        return "Recipient{accountName='" + AccountName + "', email='" + Email + "'}";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScheduledNotifications is null)
        {
            yield break;
        }

        foreach (var pair in ScheduledNotifications)
        {
            if (pair.Value is null)
            {
                yield return new ValidationResult("scheduledNotifications invalid");
                continue;
            }

            var results = new List<ValidationResult>();
            var context = new ValidationContext(pair.Value);
            if (!Validator.TryValidateObject(pair.Value, context, results, validateAllProperties: true))
            {
                foreach (var result in results)
                {
                    yield return result;
                }
            }
        }
    }
}
