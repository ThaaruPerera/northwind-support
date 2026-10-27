using System.Text;

namespace SupportDesk.Application.Authentication;

/// <summary>
/// How sign in tokens are signed, who they are for and how long they last. Bound from the
/// <c>Jwt</c> configuration section, so none of it is a literal in the code.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>HS256 wants a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>
    /// There are no refresh tokens, so the lifetime is the only limit on a token that has leaked.
    /// A day is the most a token can be asked to live.
    /// </summary>
    public const int MaximumLifetimeMinutes = 24 * 60;

    /// <summary>The secret that signs tokens. Comes from configuration or the environment, never from code.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>How long a token is valid for after it is issued, in minutes.</summary>
    public int LifetimeMinutes { get; set; }

    public TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes);

    /// <summary>Stops the API at startup, with every problem listed, instead of failing on the first sign in.</summary>
    /// <exception cref="InvalidOperationException">A setting is missing or out of range.</exception>
    public void Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            problems.Add("Jwt:SigningKey is not set. Provide it through configuration or the Jwt__SigningKey environment variable.");
        }
        else if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumKeyBytes)
        {
            problems.Add($"Jwt:SigningKey must be at least {MinimumKeyBytes} bytes long.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            problems.Add("Jwt:Issuer must be set.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            problems.Add("Jwt:Audience must be set.");
        }

        if (LifetimeMinutes is < 1 or > MaximumLifetimeMinutes)
        {
            problems.Add($"Jwt:LifetimeMinutes must be between 1 and {MaximumLifetimeMinutes}.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The Jwt configuration section is invalid: " + string.Join(" ", problems));
        }
    }
}
