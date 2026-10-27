using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// The agent with this email, or null. Upper and lower case are the same email, and spaces around
    /// it are ignored.
    /// </summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Every agent as the assignment rules see them, with how many open tickets each already has.
    /// One query for the whole team, never one per agent.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetAssignmentCandidatesAsync(CancellationToken ct);
}
