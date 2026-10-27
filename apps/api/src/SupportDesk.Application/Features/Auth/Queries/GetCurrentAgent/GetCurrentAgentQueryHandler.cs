using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Who the token belongs to. It looks the agent up again, so an agent who has been deactivated
/// since signing in is refused here even though their token has not expired.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    public const string FailureMessage = "Your session is no longer valid. Sign in again.";

    /// <exception cref="AuthenticationFailedException">No one is signed in, or the agent is gone or inactive.</exception>
    public async Task<CurrentAgentDto> HandleAsync(CancellationToken ct)
    {
        var agentId = currentUser.AgentId ?? throw new AuthenticationFailedException(FailureMessage);

        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new AuthenticationFailedException(FailureMessage);
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
