using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Resolves the roles and local groups a directory user receives from the configured
/// <see cref="DirectoryGroupMapping"/>s. A mapping whose <see cref="DirectoryGroupMapping.DirectoryGroup"/> contains
/// <c>=</c> is compared as full distinguished name (normalised, case-insensitive); otherwise it is compared with the
/// common name (first RDN value) of each group the user belongs to.
/// </summary>
public static class DirectoryRoleMapper
{
    /// <summary>Maps the user's directory groups to roles and local groups.</summary>
    /// <param name="memberOf">The distinguished names of the user's directory groups.</param>
    /// <param name="mappings">The configured mappings (inactive ones are ignored).</param>
    /// <returns>The mapped role and group identifiers.</returns>
    public static DirectoryMappingResult Map(IEnumerable<string> memberOf, IEnumerable<DirectoryGroupMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(memberOf);
        ArgumentNullException.ThrowIfNull(mappings);

        var normalizedDns = new HashSet<string>(StringComparer.Ordinal);
        var commonNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dn in memberOf)
        {
            var normalized = DistinguishedName.Normalize(dn);
            if (normalized.Length == 0)
            {
                continue;
            }

            normalizedDns.Add(normalized);
            if (DistinguishedName.GetLeafValue(dn) is { Length: > 0 } cn)
            {
                commonNames.Add(cn);
            }
        }

        var roles = new HashSet<Guid>();
        var groups = new HashSet<Guid>();
        foreach (var mapping in mappings)
        {
            if (!mapping.IsActive || !Matches(mapping.DirectoryGroup, normalizedDns, commonNames))
            {
                continue;
            }

            if (mapping.RoleId is { } roleId)
            {
                roles.Add(roleId);
            }

            if (mapping.GroupId is { } groupId)
            {
                groups.Add(groupId);
            }
        }

        return new DirectoryMappingResult(roles, groups);
    }

    /// <summary>Checks whether a mapping's directory group matches one of the user's groups.</summary>
    /// <param name="directoryGroup">The configured directory group (DN or common name).</param>
    /// <param name="memberOf">The user's group DNs.</param>
    /// <returns><see langword="true"/> when matched.</returns>
    public static bool Matches(string directoryGroup, IEnumerable<string> memberOf)
    {
        var dns = memberOf.ToList();
        return Matches(
            directoryGroup,
            dns.Select(DistinguishedName.Normalize).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal),
            dns.Select(DistinguishedName.GetLeafValue).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static bool Matches(string? directoryGroup, HashSet<string> normalizedDns, HashSet<string> commonNames)
    {
        var configured = directoryGroup?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            return false;
        }

        return configured.Contains('=')
            ? normalizedDns.Contains(DistinguishedName.Normalize(configured))
            : commonNames.Contains(configured);
    }
}
