using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Hashes passwords with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with
/// HMAC-SHA512 and a random salt for every hash, in a format that records how it was made.
/// A vetted implementation, not a home made one.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    /// <summary>OWASP's guidance for PBKDF2 with HMAC-SHA512.</summary>
    private const int IterationCount = 210_000;

    // The default hasher ignores the user; it only exists because the Identity API asks for one.
    private static readonly object NoUser = new();

    private readonly PasswordHasher<object> _hasher = new(
        Options.Create(new PasswordHasherOptions { IterationCount = IterationCount }));

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(NoUser, passwordHash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Something that is not a hash at all is simply not a match.
            return false;
        }
    }
}
