using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How long a ticket may take to be answered, and the sentence that explains why.
/// </summary>
public sealed record SlaWindow(TimeSpan Duration, string Reason);

/// <summary>
/// Works out a ticket's response window (BR-2, BR-3). The one place that rule lives: raising a
/// ticket and escalating one both ask here.
/// </summary>
public sealed class SlaPolicy(SlaSettings settings)
{
    public SlaWindow WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var baseHours = settings.BaseWindowHours[priority];
        var reason = $"{priority} priority has a base window of {Describe(baseHours)}";

        if (tier != CustomerTier.Premium)
        {
            return new SlaWindow(TimeSpan.FromHours(baseHours), reason + ".");
        }

        var reducedHours = baseHours * settings.PremiumCustomerMultiplier;

        if (reducedHours >= settings.MinimumWindowHours)
        {
            return new SlaWindow(
                TimeSpan.FromHours(reducedHours),
                $"{reason}; a Premium customer gets {Describe(reducedHours)}.");
        }

        return new SlaWindow(
            TimeSpan.FromHours(settings.MinimumWindowHours),
            $"{reason}; a Premium customer would get {Describe(reducedHours)}, " +
            $"raised to the {Describe(settings.MinimumWindowHours)} minimum.");
    }

    /// <summary>The due date for a window that starts at <paramref name="startedAtUtc"/>.</summary>
    public static DateTime DueAt(DateTime startedAtUtc, SlaWindow window) => startedAtUtc + window.Duration;

    private static string Describe(double hours) =>
        hours >= 1 && hours == Math.Floor(hours)
            ? $"{hours:0}h"
            : $"{TimeSpan.FromHours(hours).TotalMinutes:0.##} min";
}
