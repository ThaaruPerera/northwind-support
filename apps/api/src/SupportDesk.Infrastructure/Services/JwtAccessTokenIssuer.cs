using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Authentication;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Issues the signed JWT an agent presents after signing in. The key, the issuer, the audience and
/// the lifetime all come from <see cref="JwtSettings"/>; the time comes from the clock, so a test
/// can fix it.
/// </summary>
public sealed class JwtAccessTokenIssuer(JwtSettings settings, IClock clock) : IAccessTokenIssuer
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(int agentId, string fullName, string email)
    {
        var issuedAt = clock.UtcNow;

        // A token's expiry is whole seconds, so the response says the same moment the token does.
        var expiresAt = issuedAt + settings.Lifetime;
        expiresAt = expiresAt.AddTicks(-(expiresAt.Ticks % TimeSpan.TicksPerSecond));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agentId.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, fullName),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _credentials
        });

        return new AccessToken(token, expiresAt);
    }
}
