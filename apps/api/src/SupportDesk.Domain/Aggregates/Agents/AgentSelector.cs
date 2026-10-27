using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// Who a ticket goes to. <see cref="Agent"/> is <c>null</c> when nobody is eligible, which is a
/// normal outcome, and <see cref="Reason"/> says why in either case.
/// </summary>
public sealed record AssignmentDecision(AgentCandidate? Agent, string Reason);

/// <summary>
/// The assignment rules (BR-4, BR-5). Raising a ticket and escalating one both choose here, so an
/// agent is "eligible" in exactly one way.
/// </summary>
public static class AgentSelector
{
    /// <summary>
    /// Eligible means active, specialised where the category requires it, and strictly below the
    /// open ticket limit. Of those, the agent with the fewest open tickets wins; a tie goes to the
    /// lowest agent id, so the same inputs always give the same answer.
    /// </summary>
    public static AssignmentDecision Select(IEnumerable<AgentCandidate> candidates, CategoryRequirement category)
    {
        var active = candidates.Where(c => c.IsActive).ToList();

        var qualified = active
            .Where(c => !category.RequiresSpecialist || c.Specializes(category.CategoryId))
            .ToList();

        var eligible = qualified.Where(c => c.HasRoom).ToList();

        if (eligible.Count == 0)
        {
            return new AssignmentDecision(null, WhyNobodyIsEligible(qualified, category));
        }

        var chosen = eligible
            .OrderBy(c => c.OpenTicketCount)
            .ThenBy(c => c.Id)
            .First();

        var pool = category.RequiresSpecialist ? $"{category.Name} specialists" : "agents";

        return new AssignmentDecision(
            chosen,
            $"{chosen.FullName} has the fewest open tickets ({chosen.OpenTicketCount}) " +
            $"of {eligible.Count} eligible {pool}.");
    }

    /// <summary>
    /// Re-evaluates the owner of a ticket that is being escalated: the current agent keeps it if
    /// they are still eligible, otherwise it is assigned afresh. The ticket itself is already in
    /// the agent's open count, so it is not counted against them again; otherwise an agent one
    /// below their limit would lose a ticket they already hold.
    /// </summary>
    public static AssignmentDecision SelectForEscalation(
        int? currentAgentId,
        IReadOnlyCollection<AgentCandidate> candidates,
        CategoryRequirement category)
    {
        var current = currentAgentId is null ? null : candidates.FirstOrDefault(c => c.Id == currentAgentId);

        if (current is null)
        {
            return Select(candidates, category);
        }

        var stillQualified = current.IsActive
            && (!category.RequiresSpecialist || current.Specializes(category.CategoryId))
            && current.OpenTicketCount - 1 < current.MaxOpenTickets;

        if (stillQualified)
        {
            return new AssignmentDecision(current, $"{current.FullName} keeps the ticket; still eligible.");
        }

        var replacement = Select(candidates.Where(c => c.Id != current.Id), category);

        return replacement with { Reason = $"{current.FullName} is no longer eligible. {replacement.Reason}" };
    }

    private static string WhyNobodyIsEligible(List<AgentCandidate> qualified, CategoryRequirement category)
    {
        if (qualified.Count == 0)
        {
            return category.RequiresSpecialist
                ? $"No active agent specialises in {category.Name}."
                : "There are no active agents.";
        }

        return category.RequiresSpecialist
            ? $"Every active {category.Name} specialist is at their open ticket limit."
            : "Every active agent is at their open ticket limit.";
    }
}
