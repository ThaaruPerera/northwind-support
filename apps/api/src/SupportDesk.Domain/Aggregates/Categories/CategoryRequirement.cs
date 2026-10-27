namespace SupportDesk.Domain.Aggregates.Categories;

/// <summary>
/// What the triage rules need to know about a ticket's category, read from the category's own
/// flags. The rules never look at a category's name to decide anything; it is only used in the
/// sentences that explain a decision.
/// </summary>
public sealed record CategoryRequirement(
    int CategoryId,
    string Name,
    bool RequiresSpecialist,
    bool ForcesCriticalPriority);
