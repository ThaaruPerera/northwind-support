namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into something safe to store, and checks a password against it later. The rest
/// of the application never needs to know how.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>A salted hash of the password. Hashing the same password twice gives different results.</summary>
    string Hash(string password);

    /// <summary>True when <paramref name="password"/> is the one <paramref name="passwordHash"/> was made from.</summary>
    bool Verify(string passwordHash, string password);
}
