using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>The priority a ticket ends up with, and the sentence that explains why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);

/// <summary>
/// The priority rules (BR-1, and the one step raise of BR-7). Which categories force Critical is
/// read from the category's own flag, never from its name.
/// </summary>
public static class PriorityPolicy
{
    public static PriorityDecision Decide(TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            return new PriorityDecision(
                TicketPriority.Critical,
                $"Critical, because {category.Name} tickets are always Critical.");
        }

        if (requested is { } priority)
        {
            return new PriorityDecision(priority, $"{priority}, as requested.");
        }

        return new PriorityDecision(TicketPriority.Medium, "Medium, because no priority was requested.");
    }

    /// <summary>One level up, or <c>null</c> when the ticket is already as high as it goes.</summary>
    public static TicketPriority? Raise(TicketPriority priority) =>
        priority == Enum.GetValues<TicketPriority>().Max() ? null : priority + 1;
}
