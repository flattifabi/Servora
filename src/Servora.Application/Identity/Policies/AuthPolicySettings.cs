namespace Servora.Application.Identity.Policies;

/// <summary>
/// Authentication policy configured on <c>/admin/auth</c>: password rules, lockout, session lifetime and whether
/// local (non-directory) accounts may sign in. Stored as the system property <see cref="SettingKey"/>.
/// </summary>
public sealed record AuthPolicySettings
{
    /// <summary>The system property key.</summary>
    public const string SettingKey = "identity.auth.policy";

    /// <summary>Gets or sets the minimum password length (8–128).</summary>
    public int MinimumPasswordLength { get; set; } = 10;

    /// <summary>Gets or sets a value indicating whether a digit is required.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether upper and lower case letters are required.</summary>
    public bool RequireMixedCase { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a symbol is required.</summary>
    public bool RequireSymbol { get; set; }

    /// <summary>Gets or sets the number of consecutive failed sign-ins before the account is locked (1–100).</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Gets or sets the lockout duration in minutes (1–1440).</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Gets or sets the sliding session lifetime in minutes for normal sign-ins (5–10080).</summary>
    public int SessionLifetimeMinutes { get; set; } = 480;

    /// <summary>Gets or sets the session lifetime in days when "Keep me signed in" is chosen (1–90).</summary>
    public int RememberMeDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets a value indicating whether local accounts may sign in with a password. The bootstrap
    /// <c>admin</c> account can always sign in locally (break-glass access).
    /// </summary>
    public bool AllowLocalLogin { get; set; } = true;

    /// <summary>Converts the settings to the password policy used for validation and lockout.</summary>
    /// <returns>The password policy.</returns>
    public PasswordPolicy ToPasswordPolicy() => new()
    {
        MinimumLength = MinimumPasswordLength,
        RequireDigit = RequireDigit,
        RequireMixedCase = RequireMixedCase,
        RequireSymbol = RequireSymbol,
        MaxFailedAttempts = MaxFailedAttempts,
        LockoutDuration = TimeSpan.FromMinutes(LockoutMinutes),
    };

    /// <summary>Validates the value ranges.</summary>
    /// <returns><see langword="null"/> when valid, otherwise a user-safe message.</returns>
    public string? Validate()
    {
        if (MinimumPasswordLength is < 8 or > 128)
        {
            return "The minimum password length must be between 8 and 128 characters.";
        }

        if (MaxFailedAttempts is < 1 or > 100)
        {
            return "The lockout threshold must be between 1 and 100 failed attempts.";
        }

        if (LockoutMinutes is < 1 or > 1440)
        {
            return "The lockout duration must be between 1 and 1440 minutes.";
        }

        if (SessionLifetimeMinutes is < 5 or > 10080)
        {
            return "The session lifetime must be between 5 minutes and 7 days.";
        }

        if (RememberMeDays is < 1 or > 90)
        {
            return "The \"keep me signed in\" lifetime must be between 1 and 90 days.";
        }

        return null;
    }
}
