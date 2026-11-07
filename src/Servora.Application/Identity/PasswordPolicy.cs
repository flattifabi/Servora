namespace Servora.Application.Identity;

/// <summary>
/// Password rules for local accounts. Defaults follow NIST SP 800-63B (length over complexity); the Identity module
/// may load overrides from system properties.
/// </summary>
public sealed record PasswordPolicy
{
    /// <summary>Gets the default policy.</summary>
    public static PasswordPolicy Default { get; } = new();

    /// <summary>Gets the minimum length.</summary>
    public int MinimumLength { get; init; } = 10;

    /// <summary>Gets a value indicating whether at least one digit is required.</summary>
    public bool RequireDigit { get; init; } = true;

    /// <summary>Gets a value indicating whether mixed case is required.</summary>
    public bool RequireMixedCase { get; init; } = true;

    /// <summary>Gets a value indicating whether at least one symbol (non letter / digit) is required.</summary>
    public bool RequireSymbol { get; init; }

    /// <summary>Gets the number of failed attempts before lockout.</summary>
    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>Gets the lockout duration.</summary>
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Describes the policy in one user-facing sentence, for example as help text below password inputs.</summary>
    /// <returns>The description.</returns>
    public string Describe()
    {
        var parts = new List<string>();
        if (RequireMixedCase)
        {
            parts.Add("upper and lower case letters");
        }

        if (RequireDigit)
        {
            parts.Add("a digit");
        }

        if (RequireSymbol)
        {
            parts.Add("a symbol");
        }

        var rules = parts.Count switch
        {
            0 => string.Empty,
            1 => $" with {parts[0]}",
            _ => $" with {string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
        };
        return $"At least {MinimumLength} characters{rules}. Must not contain the user name.";
    }

    /// <summary>Validates a candidate password.</summary>
    /// <param name="password">The candidate password.</param>
    /// <param name="userName">The user name (the password must not contain it).</param>
    /// <returns><see langword="null"/> when valid, otherwise a user-safe message.</returns>
    public string? Validate(string? password, string? userName = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return $"The password must be at least {MinimumLength} characters long.";
        }

        if (password.Length > 256)
        {
            return "The password must not exceed 256 characters.";
        }

        if (RequireDigit && !password.Any(char.IsDigit))
        {
            return "The password must contain at least one digit.";
        }

        if (RequireMixedCase && !(password.Any(char.IsUpper) && password.Any(char.IsLower)))
        {
            return "The password must contain upper and lower case letters.";
        }

        if (RequireSymbol && password.All(char.IsLetterOrDigit))
        {
            return "The password must contain at least one symbol.";
        }

        if (!string.IsNullOrEmpty(userName) && password.Contains(userName, StringComparison.OrdinalIgnoreCase))
        {
            return "The password must not contain the user name.";
        }

        if (string.Equals(password, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "This password is not allowed.";
        }

        return null;
    }
}
