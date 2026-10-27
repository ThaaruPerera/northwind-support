namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>An agent, as the web app needs to greet them.</summary>
public sealed record CurrentAgentDto(int Id, string FullName, string Email);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentAgentDto User);
