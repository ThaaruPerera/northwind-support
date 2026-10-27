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
}
