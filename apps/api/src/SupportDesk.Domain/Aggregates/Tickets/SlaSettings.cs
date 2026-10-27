namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The tunable numbers behind the SLA rules (BR-6). Bound from the <c>Sla</c> configuration
/// section, so a window can change without a code change.
/// </summary>
public sealed class SlaSettings
{
    /// <summary>The response window, in hours, for each priority.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    /// <summary>What a Premium customer's window is multiplied by.</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>The shortest window a Premium customer can be given, in hours.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>A ticket is at risk once this percentage of its window, or less, is left.</summary>
    public decimal AtRiskThresholdPercent { get; set; }

    /// <summary>Fails early, with every problem listed, instead of at the first ticket raised.</summary>
    /// <exception cref="InvalidOperationException">A setting is missing or out of range.</exception>
    public void Validate()
    {
        var problems = new List<string>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                problems.Add($"BaseWindowHours:{priority} must be set to more than zero.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            problems.Add("PremiumCustomerMultiplier must be more than 0 and at most 1.");
        }

        if (MinimumWindowHours <= 0)
        {
            problems.Add("MinimumWindowHours must be more than zero.");
        }

        if (AtRiskThresholdPercent is < 0 or > 100)
        {
            problems.Add("AtRiskThresholdPercent must be between 0 and 100.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The Sla configuration section is invalid: " + string.Join(" ", problems));
        }
    }
}
