namespace Servora.Installer.Core;

/// <summary>Formats and compares semantic product versions such as <c>1.2.0</c>, <c>1.3.0-beta.2+5f3c1a</c>.</summary>
internal static class ProductVersion
{
    /// <summary>Removes build metadata (<c>+commit</c>) from a version for display.</summary>
    /// <param name="version">The raw informational version.</param>
    /// <returns>The display version, or <c>unknown</c>.</returns>
    public static string Display(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "unknown";
        }

        var plus = version.IndexOf('+');
        return (plus >= 0 ? version[..plus] : version).Trim();
    }

    /// <summary>
    /// Compares two versions by semantic versioning precedence (numeric core first; a pre-release sorts before the
    /// release; build metadata is ignored).
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>Negative when <paramref name="left"/> is older, zero when equal, positive when newer; <see langword="null"/> when either version cannot be parsed.</returns>
    public static int? Compare(string? left, string? right)
    {
        if (!TryParse(left, out var leftCore, out var leftPre) || !TryParse(right, out var rightCore, out var rightPre))
        {
            return null;
        }

        var core = leftCore.CompareTo(rightCore);
        if (core != 0)
        {
            return core;
        }

        if (leftPre.Length == 0 || rightPre.Length == 0)
        {
            return (leftPre.Length == 0 ? 1 : 0) - (rightPre.Length == 0 ? 1 : 0);
        }

        var leftParts = leftPre.Split('.');
        var rightParts = rightPre.Split('.');
        for (var i = 0; i < Math.Min(leftParts.Length, rightParts.Length); i++)
        {
            var leftNumeric = int.TryParse(leftParts[i], out var leftNumber);
            var rightNumeric = int.TryParse(rightParts[i], out var rightNumber);
            var part = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(leftParts[i], rightParts[i]),
            };
            if (part != 0)
            {
                return part;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static bool TryParse(string? text, out Version core, out string preRelease)
    {
        core = new Version(0, 0);
        preRelease = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = Display(text);
        var dash = value.IndexOf('-');
        var corePart = dash >= 0 ? value[..dash] : value;
        preRelease = dash >= 0 ? value[(dash + 1)..] : string.Empty;
        if (!Version.TryParse(corePart, out var parsed))
        {
            return false;
        }

        // Normalise 1.2 / 1.2.0 / 1.2.0.0 so that they compare equal.
        core = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }
}
