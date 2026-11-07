namespace Servora.Application.Security;

/// <summary>Hashes and verifies local account passwords using the ASP.NET Core Identity PBKDF2 hasher.</summary>
public interface IPasswordHasherService
{
    /// <summary>Hashes a password.</summary>
    /// <param name="password">The plain password.</param>
    /// <returns>The hash.</returns>
    string Hash(string password);

    /// <summary>Verifies a password against a hash in constant time.</summary>
    /// <param name="hash">The stored hash.</param>
    /// <param name="password">The supplied password.</param>
    /// <returns><see langword="true"/> when the password matches.</returns>
    bool Verify(string hash, string password);
}
