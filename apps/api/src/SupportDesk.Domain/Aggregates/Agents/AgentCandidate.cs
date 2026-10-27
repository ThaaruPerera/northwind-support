namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// An agent as the assignment rules see them: who they are, what they can take, and how much
/// they already hold. <paramref name="OpenTicketCount"/> is every ticket that is not Resolved or
/// Closed.
/// </summary>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SpecializationCategoryIds)
{
    /// <summary>Strictly below the limit: an agent at their limit takes nothing more.</summary>
    public bool HasRoom => OpenTicketCount < MaxOpenTickets;

    public bool Specializes(int categoryId) => SpecializationCategoryIds.Contains(categoryId);
}
