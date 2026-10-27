using System.Linq.Expressions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// The SLA statuses as conditions the database can evaluate, so a list can be filtered, counted and
/// paged by SLA status without loading it. They are the same rules as
/// <see cref="SlaEvaluator"/>, written as SQL; a test checks that the two agree on every ticket.
/// </summary>
public static class SlaStatusFilter
{
    /// <param name="status">The status to keep.</param>
    /// <param name="nowUtc">The moment to judge against, passed in so it can be faked.</param>
    /// <param name="atRiskThresholdPercent">A ticket is at risk with this much of its window left, or less.</param>
    public static Expression<Func<Ticket, bool>> For(SlaStatus status, DateTime nowUtc, decimal atRiskThresholdPercent)
    {
        var threshold = (double)atRiskThresholdPercent;

        return status switch
        {
            SlaStatus.NotApplicable => t => t.DueAtUtc == null,

            SlaStatus.Met => t =>
                t.DueAtUtc != null && t.ResolvedAtUtc != null && t.ResolvedAtUtc <= t.DueAtUtc,

            SlaStatus.Breached => t =>
                t.DueAtUtc != null &&
                (t.ResolvedAtUtc != null ? t.ResolvedAtUtc > t.DueAtUtc : nowUtc > t.DueAtUtc),

            // Open and not yet due, with the time left at or under the threshold share of the window.
            SlaStatus.AtRisk => t =>
                t.DueAtUtc != null && t.ResolvedAtUtc == null && nowUtc <= t.DueAtUtc &&
                SqlFunctions.SecondsBetween(nowUtc, t.DueAtUtc.Value) * 100.0 <=
                SqlFunctions.SecondsBetween(t.SlaStartedAtUtc, t.DueAtUtc.Value) * threshold,

            SlaStatus.WithinSla => t =>
                t.DueAtUtc != null && t.ResolvedAtUtc == null && nowUtc <= t.DueAtUtc &&
                SqlFunctions.SecondsBetween(nowUtc, t.DueAtUtc.Value) * 100.0 >
                SqlFunctions.SecondsBetween(t.SlaStartedAtUtc, t.DueAtUtc.Value) * threshold,

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.")
        };
    }
}
