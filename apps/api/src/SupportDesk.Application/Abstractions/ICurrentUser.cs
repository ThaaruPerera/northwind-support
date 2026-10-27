namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Who is making the current request, as the token says. Handlers ask this instead of touching
/// HTTP, so they stay testable and cannot be told who to act as by a request body.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed in agent's id, or null when the request carries no valid sign in.</summary>
    int? AgentId { get; }
}
