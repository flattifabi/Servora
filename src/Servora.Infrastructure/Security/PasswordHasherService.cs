using Microsoft.AspNetCore.Identity;
using Servora.Application.Security;

namespace Servora.Infrastructure.Security;

/// <summary>
/// <see cref="IPasswordHasherService"/> based on the ASP.NET Core Identity <see cref="PasswordHasher{TUser}"/>
/// (PBKDF2 with HMAC-SHA512, 100,000 iterations, random salt). No custom cryptography.
/// </summary>
public sealed class PasswordHasherService : IPasswordHasherService
{
    private static readonly object s_user = new();
    private readonly PasswordHasher<object> _hasher = new();

    /// <inheritdoc />
    public string Hash(string password) => _hasher.HashPassword(s_user, password);

    /// <inheritdoc />
    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(s_user, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
