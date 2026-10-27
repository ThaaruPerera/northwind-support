using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with their email and password, and gives them a token.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokens,
    ILogger<LoginCommandHandler> logger)
{
    /// <summary>The one thing a failed sign in ever says, whatever the reason.</summary>
    public const string FailureMessage = "Invalid email or password.";

    /// <exception cref="AuthenticationFailedException">
    /// The email is unknown, the password is wrong, or the agent is inactive. All three look the same.
    /// </exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email, ct);

        // One password is always checked against one hash, a real one or a dummy, and before anything
        // else is decided. Otherwise a wrong email would answer faster than a wrong password and
        // tell an attacker which emails exist.
        var passwordMatches = passwordHasher.Verify(
            agent?.PasswordHash ?? passwordHasher.DummyHash,
            request.Password);

        if (agent is null || agent.PasswordHash is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogInformation("A sign in was refused ({Reason}).", Describe(agent, passwordMatches));

            throw new AuthenticationFailedException(FailureMessage);
        }

        var token = tokens.Issue(agent.Id, agent.FullName, agent.Email);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, new CurrentAgentDto(agent.Id, agent.FullName, agent.Email));
    }

    /// <summary>The real reason, for the server log only. It is never sent to the caller.</summary>
    private static string Describe(Domain.Aggregates.Agents.Agent? agent, bool passwordMatches) =>
        agent is null ? "unknown email"
        : agent.PasswordHash is null ? "no password set"
        : !passwordMatches ? "wrong password"
        : "inactive agent";
}
