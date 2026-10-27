namespace SupportDesk.Application.Abstractions;

/// <summary>A signed token and the moment it stops being valid.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Makes the token an agent presents on every request after signing in.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(int agentId, string fullName, string email);
}
