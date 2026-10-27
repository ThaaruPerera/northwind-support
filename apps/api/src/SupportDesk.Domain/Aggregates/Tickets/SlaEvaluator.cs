namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    private static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : SlaStatus.WithinSla;

    /// <summary>
    /// The full BR-8 status. The window is the time from <paramref name="slaStartedAtUtc"/> to the
    /// due date, and a ticket is at risk once <paramref name="atRiskThresholdPercent"/> of it, or
    /// less, is left. Exactly at the due date it is still at risk; one tick later it is breached.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime slaStartedAtUtc,
        decimal atRiskThresholdPercent)
    {
        var status = Evaluate(dueAtUtc, resolvedAtUtc, nowUtc);

        if (status != SlaStatus.WithinSla)
        {
            return status;
        }

        var window = dueAtUtc!.Value - slaStartedAtUtc;
        var remaining = dueAtUtc.Value - nowUtc;

        // Ticks and decimals, so a boundary lands exactly on one side and not near it.
        return remaining.Ticks * 100m <= window.Ticks * atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
