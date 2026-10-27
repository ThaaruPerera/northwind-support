using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Services;

/// <summary>
/// What triage decided for a ticket: its priority, the SLA window and due date that follow from
/// it, and who gets it. Each decision carries the sentence that explains it.
/// </summary>
public sealed record TriageDecision(
    PriorityDecision Priority,
    SlaWindow Window,
    DateTime DueAtUtc,
    AssignmentDecision Assignment);

/// <summary>
/// What escalation decided: the next priority, the new window and due date counted from the
/// moment of escalation, and who owns the ticket afterwards.
/// </summary>
public sealed record EscalationDecision(
    TicketPriority Priority,
    SlaWindow Window,
    DateTime DueAtUtc,
    AssignmentDecision Assignment);

/// <summary>
/// Puts the three triage rules in order, so raising a ticket asks one question and gets one
/// answer. Each rule lives in its own class; this only decides how their results feed each other:
/// the priority sets the window, and the owner is chosen on the category.
/// </summary>
public sealed class TriageService(SlaPolicy slaPolicy)
{
    public TriageDecision Triage(
        TicketPriority? requestedPriority,
        CategoryRequirement category,
        CustomerTier tier,
        IReadOnlyCollection<AgentCandidate> agents,
        DateTime nowUtc)
    {
        var priority = PriorityPolicy.Decide(requestedPriority, category);
        var window = slaPolicy.WindowFor(priority.Priority, tier);
        var assignment = AgentSelector.Select(agents, category);

        return new TriageDecision(priority, window, SlaPolicy.DueAt(nowUtc, window), assignment);
    }

    /// <summary>
    /// The same rules, applied to a ticket that already exists: one priority up, a window worked out
    /// for that priority and counted from <paramref name="nowUtc"/>, and the current owner kept if
    /// they are still eligible.
    /// </summary>
    /// <exception cref="InvalidOperationException">The priority cannot go any higher; the ticket
    /// should have been refused before it got here.</exception>
    public EscalationDecision Escalate(
        TicketPriority currentPriority,
        CategoryRequirement category,
        CustomerTier tier,
        int? currentAgentId,
        IReadOnlyCollection<AgentCandidate> agents,
        DateTime nowUtc)
    {
        var priority = PriorityPolicy.Raise(currentPriority)
            ?? throw new InvalidOperationException($"{currentPriority} is the highest priority.");

        var window = slaPolicy.WindowFor(priority, tier);
        var assignment = AgentSelector.SelectForEscalation(currentAgentId, agents, category);

        return new EscalationDecision(priority, window, SlaPolicy.DueAt(nowUtc, window), assignment);
    }
}
