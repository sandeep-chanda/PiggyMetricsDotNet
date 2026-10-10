using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain.Timeseries;

public sealed class ItemMetric
{
    public ItemMetric()
    {
    }

    public ItemMetric(string title, decimal amount)
    {
        Title = title;
        Amount = amount;
    }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o))
        {
            return true;
        }

        if (o is not ItemMetric that)
        {
            return false;
        }

        // Java ItemMetric.equals compares title with equalsIgnoreCase, while hashCode uses title.hashCode().
        return string.Equals(Title, that.Title, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        // Deliberately the Java hash: case-sensitive, so it does not match Equals.
        return Title?.GetHashCode() ?? 0;
    }
}
