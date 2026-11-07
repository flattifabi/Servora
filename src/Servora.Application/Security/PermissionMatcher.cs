namespace Servora.Application.Security;

/// <summary>
/// Evaluates permission keys against a granted set. Supports the global wildcard <c>*</c> and prefix wildcards such
/// as <c>incident.*</c>.
/// </summary>
public static class PermissionMatcher
{
    /// <summary>Checks whether <paramref name="required"/> is covered by <paramref name="granted"/>.</summary>
    /// <param name="granted">The granted permission keys.</param>
    /// <param name="required">The required permission key.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    public static bool IsGranted(IReadOnlySet<string> granted, string required)
    {
        if (granted.Contains("*") || granted.Contains(required))
        {
            return true;
        }

        var index = required.LastIndexOf('.');
        while (index > 0)
        {
            if (granted.Contains(string.Concat(required.AsSpan(0, index), ".*")))
            {
                return true;
            }

            index = required.LastIndexOf('.', index - 1);
        }

        return false;
    }
}
