using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>Default <see cref="IDirectoryAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="store">The identity settings store.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="configuration">The directory configuration provider.</param>
/// <param name="client">The directory client.</param>
/// <param name="sync">The synchronisation service.</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
public sealed class DirectoryAdminService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IdentitySettingsStore store,
    ISecretStore secrets,
    IDirectoryConfigurationProvider configuration,
    IDirectoryClient client,
    IDirectorySyncService sync,
    IPermissionService permissions,
    IAuditService audit) : IDirectoryAdminService
{
    private const int MaxBindPasswordLength = 1024;

    /// <inheritdoc />
    public async Task<Result<DirectoryOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<DirectoryOverview>.Forbidden();
        }

        var settings = await configuration.GetSettingsAsync(cancellationToken);
        var hasPassword = await secrets.ExistsAsync(DirectorySettings.BindPasswordSecretName, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var directoryUsers = await db.Set<User>().CountAsync(u => u.Source == UserSource.Directory, cancellationToken);
        var windowsUsers = await db.Set<User>().CountAsync(u => u.Source == UserSource.Windows, cancellationToken);
        var mappings = await db.Set<DirectoryGroupMapping>().CountAsync(cancellationToken);
        var lastRun = await sync.GetLastRunAsync(cancellationToken);
        return Result<DirectoryOverview>.Success(new DirectoryOverview(settings, hasPassword, directoryUsers, windowsUsers, mappings, lastRun));
    }

    /// <inheritdoc />
    public async Task<Result> SaveSettingsAsync(DirectorySettings settings, string? newBindPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        var normalized = Normalize(settings);
        if (DirectorySettingsValidator.Validate(normalized) is { } error)
        {
            return Result.Invalid(error);
        }

        if (newBindPassword is { Length: > MaxBindPasswordLength })
        {
            return Result.Invalid("The bind password is too long.");
        }

        var saved = await configuration.GetSettingsAsync(cancellationToken);
        var hasPassword = await secrets.ExistsAsync(DirectorySettings.BindPasswordSecretName, cancellationToken);
        var passwordProvided = !string.IsNullOrEmpty(newBindPassword);
        if (!passwordProvided && hasPassword && normalized.HasDifferentConnectionTarget(saved) && saved.IsConnectionConfigured)
        {
            return Result.Invalid("Re-enter the bind password when you change the server, port, security mode, certificate validation or bind account.");
        }

        if (normalized.Enabled && !passwordProvided && !hasPassword)
        {
            return Result.Invalid("Enter the bind password of the service account before enabling the directory integration.");
        }

        if (passwordProvided)
        {
            await secrets.SetAsync(DirectorySettings.BindPasswordSecretName, newBindPassword!, "LDAP / Active Directory service account password", cancellationToken);
        }

        await store.SaveAsync(DirectorySettings.SettingKey, normalized, "LDAP / Active Directory and Windows sign-in settings.", cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "directory.settings.updated")
        {
            TargetType = "sys_properties",
            TargetName = DirectorySettings.SettingKey,
            Message = normalized.Security == LdapSecurityMode.None && normalized.Enabled ? "Warning: unencrypted LDAP is enabled." : null,
            Details = new { before = saved, after = normalized, bindPasswordChanged = passwordProvided },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<DirectoryTestResult>> TestConnectionAsync(DirectorySettings settings, string? newBindPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<DirectoryTestResult>.Forbidden();
        }

        var normalized = Normalize(settings);
        var prepared = await PrepareAsync(normalized, newBindPassword, cancellationToken);
        if (!prepared.Succeeded)
        {
            return Result<DirectoryTestResult>.From(prepared);
        }

        var result = await client.TestConnectionAsync(normalized, prepared.Value, cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "directory.test_connection", result.Succeeded ? Domain.Observability.AuditOutcome.Success : Domain.Observability.AuditOutcome.Failure)
        {
            TargetName = $"{normalized.Host}:{normalized.Port}",
            Message = result.Message,
        }, cancellationToken);
        return Result<DirectoryTestResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<DirectoryUserPreview>> TestUserLookupAsync(DirectorySettings settings, string? newBindPassword, string loginName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<DirectoryUserPreview>.Forbidden();
        }

        var login = DirectoryLoginName.Parse(loginName);
        if (login is null)
        {
            return Result<DirectoryUserPreview>.Invalid("Enter a login name such as jane.doe, CORP\\jane.doe or jane.doe@corp.example.com.");
        }

        var normalized = Normalize(settings);
        var prepared = await PrepareAsync(normalized, newBindPassword, cancellationToken);
        if (!prepared.Succeeded)
        {
            return Result<DirectoryUserPreview>.From(prepared);
        }

        var lookup = await client.FindUserAsync(normalized, prepared.Value, login, cancellationToken);
        if (!lookup.Succeeded)
        {
            return lookup.Outcome == DirectoryLookupOutcome.UserNotFound
                ? Result<DirectoryUserPreview>.NotFound(lookup.Message ?? "No unique user matches this login name.")
                : Result<DirectoryUserPreview>.Failure(lookup.Message ?? "The directory lookup failed.");
        }

        var entry = lookup.Entry!;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mappings = await db.Set<DirectoryGroupMapping>().AsNoTracking().Where(m => m.IsActive).Take(DirectoryConfigurationProvider.MaxMappings).ToListAsync(cancellationToken);
        var mapped = DirectoryRoleMapper.Map(entry.Groups, mappings);
        var roleLabels = await db.Set<Role>().AsNoTracking().Where(r => mapped.RoleIds.Contains(r.Id)).OrderBy(r => r.Label).Select(r => r.Label).ToListAsync(cancellationToken);
        var groupNames = await db.Set<Group>().AsNoTracking().Where(g => mapped.GroupIds.Contains(g.Id)).OrderBy(g => g.Name).Select(g => g.Name).ToListAsync(cancellationToken);

        var normalizedName = entry.UserName.ToUpperInvariant();
        var existing = await db.Set<User>().AsNoTracking()
            .Where(u => (entry.ExternalId != null && u.Source == UserSource.Directory && u.ExternalId == entry.ExternalId) || u.NormalizedUserName == normalizedName)
            .Select(u => new { u.Id, u.Source })
            .FirstOrDefaultAsync(cancellationToken);
        string? note = existing switch
        {
            null when !normalized.JitProvisioning => "No local account exists and just-in-time provisioning is off: this user cannot sign in yet.",
            null => "No local account exists yet. It will be created at the first sign-in.",
            { Source: UserSource.Local } => "A local account with the same login name exists. It is not linked to the directory, so directory sign-in is refused for this name.",
            _ => entry.IsDisabled ? "The account is disabled in the directory." : null,
        };
        return Result<DirectoryUserPreview>.Success(new DirectoryUserPreview(entry, roleLabels, groupNames, existing?.Source == UserSource.Directory ? existing.Id : null, note));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DirectoryMappingItem>>> ListMappingsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<DirectoryMappingItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mappings = await db.Set<DirectoryGroupMapping>().AsNoTracking().OrderBy(m => m.DirectoryGroup).Take(DirectoryConfigurationProvider.MaxMappings).ToListAsync(cancellationToken);
        var roleIds = mappings.Where(m => m.RoleId.HasValue).Select(m => m.RoleId!.Value).Distinct().ToList();
        var groupIds = mappings.Where(m => m.GroupId.HasValue).Select(m => m.GroupId!.Value).Distinct().ToList();
        var roles = await db.Set<Role>().AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Label, cancellationToken);
        var groups = await db.Set<Group>().AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        IReadOnlyList<DirectoryMappingItem> items = mappings.Select(m => new DirectoryMappingItem(
            m.Id,
            m.DirectoryGroup,
            m.RoleId,
            m.RoleId is { } r ? roles.GetValueOrDefault(r) : null,
            m.GroupId,
            m.GroupId is { } g ? groups.GetValueOrDefault(g) : null,
            m.Description,
            m.IsActive)).ToList();
        return Result<IReadOnlyList<DirectoryMappingItem>>.Success(items);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveMappingAsync(DirectoryMappingEdit model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var directoryGroup = model.DirectoryGroup?.Trim() ?? string.Empty;
        if (directoryGroup.Length is 0 or > 1024)
        {
            return Result<Guid>.Invalid("Enter the directory group as distinguished name or common name (max. 1024 characters).");
        }

        if (model.RoleId is null && model.GroupId is null)
        {
            return Result<Guid>.Invalid("Select a role, a local group or both.");
        }

        if (model.Description is { Length: > 1000 })
        {
            return Result<Guid>.Invalid("The description must not exceed 1000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var requested = new List<string>();
        if (model.RoleId is { } roleId)
        {
            if (!await db.Set<Role>().AnyAsync(r => r.Id == roleId, cancellationToken))
            {
                return Result<Guid>.Invalid("The selected role does not exist.");
            }

            requested.AddRange(await IdentityGrants.GetRolePermissionsAsync(db, [roleId], cancellationToken));
        }

        if (model.GroupId is { } groupId)
        {
            if (!await db.Set<Group>().AnyAsync(g => g.Id == groupId, cancellationToken))
            {
                return Result<Guid>.Invalid("The selected group does not exist.");
            }

            requested.AddRange(await IdentityGrants.GetGroupPermissionsAsync(db, groupId, cancellationToken));
        }

        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, requested, cancellationToken);
        if (missing.Count > 0)
        {
            await AuditDeniedAsync("directory.mapping.denied", directoryGroup, missing, cancellationToken);
            return Result<Guid>.Forbidden(PrivilegeEscalationGuard.Describe(missing));
        }

        DirectoryGroupMapping? mapping;
        if (model.Id is { } id)
        {
            mapping = await db.Set<DirectoryGroupMapping>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (mapping is null)
            {
                return Result<Guid>.NotFound();
            }
        }
        else
        {
            mapping = new DirectoryGroupMapping();
            db.Set<DirectoryGroupMapping>().Add(mapping);
        }

        mapping.DirectoryGroup = directoryGroup;
        mapping.RoleId = model.RoleId;
        mapping.GroupId = model.GroupId;
        mapping.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        mapping.IsActive = model.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", model.Id is null ? "directory.mapping.created" : "directory.mapping.updated")
        {
            TargetType = "sys_directory_group_map",
            TargetId = mapping.Id.ToString(),
            TargetName = mapping.DirectoryGroup,
            Details = new { mapping.RoleId, mapping.GroupId, mapping.IsActive },
        }, cancellationToken);
        return Result<Guid>.Success(mapping.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mapping = await db.Set<DirectoryGroupMapping>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (mapping is null)
        {
            return Result.NotFound();
        }

        db.Set<DirectoryGroupMapping>().Remove(mapping);
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "directory.mapping.deleted")
        {
            TargetType = "sys_directory_group_map",
            TargetId = mapping.Id.ToString(),
            TargetName = mapping.DirectoryGroup,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<DirectorySyncSummary>> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<DirectorySyncSummary>.Forbidden();
        }

        var summary = await sync.RunAsync(DirectorySyncTrigger.Manual, cancellationToken);
        return summary.RunId == Guid.Empty
            ? Result<DirectorySyncSummary>.Invalid(summary.Message ?? "The synchronisation could not start.")
            : Result<DirectorySyncSummary>.Success(summary);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DirectorySyncSummary>>> GetSyncHistoryAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<DirectorySyncSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runs = await db.Set<DirectorySyncRun>().AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        IReadOnlyList<DirectorySyncSummary> items = runs.Select(DirectorySyncSummary.From).ToList();
        return Result<IReadOnlyList<DirectorySyncSummary>>.Success(items);
    }

    private static DirectorySettings Normalize(DirectorySettings settings) => settings with
    {
        Host = settings.Host?.Trim() ?? string.Empty,
        BaseDn = settings.BaseDn?.Trim() ?? string.Empty,
        BindDn = settings.BindDn?.Trim() ?? string.Empty,
        NetBiosDomain = string.IsNullOrWhiteSpace(settings.NetBiosDomain) ? null : settings.NetBiosDomain.Trim(),
        UserSearchBase = string.IsNullOrWhiteSpace(settings.UserSearchBase) ? null : settings.UserSearchBase.Trim(),
        GroupSearchBase = string.IsNullOrWhiteSpace(settings.GroupSearchBase) ? null : settings.GroupSearchBase.Trim(),
        UserFilter = settings.UserFilter?.Trim() ?? string.Empty,
        GroupFilter = settings.GroupFilter?.Trim() ?? string.Empty,
        UserNameAttribute = settings.UserNameAttribute?.Trim() ?? string.Empty,
        UpnAttribute = string.IsNullOrWhiteSpace(settings.UpnAttribute) ? null : settings.UpnAttribute.Trim(),
        DisplayNameAttribute = settings.DisplayNameAttribute?.Trim() ?? string.Empty,
        FirstNameAttribute = settings.FirstNameAttribute?.Trim() ?? string.Empty,
        LastNameAttribute = settings.LastNameAttribute?.Trim() ?? string.Empty,
        EmailAttribute = settings.EmailAttribute?.Trim() ?? string.Empty,
        PhoneAttribute = settings.PhoneAttribute?.Trim() ?? string.Empty,
        TitleAttribute = settings.TitleAttribute?.Trim() ?? string.Empty,
        DepartmentAttribute = settings.DepartmentAttribute?.Trim() ?? string.Empty,
        ExternalIdAttribute = settings.ExternalIdAttribute?.Trim() ?? string.Empty,
        MemberOfAttribute = settings.MemberOfAttribute?.Trim() ?? string.Empty,
        DefaultRoleName = string.IsNullOrWhiteSpace(settings.DefaultRoleName) ? null : settings.DefaultRoleName.Trim(),
    };

    private async Task<Result<string?>> PrepareAsync(DirectorySettings settings, string? newBindPassword, CancellationToken cancellationToken)
    {
        if (DirectorySettingsValidator.Validate(settings with { Enabled = true }) is { } error)
        {
            return Result<string?>.Invalid(error);
        }

        if (!string.IsNullOrEmpty(newBindPassword))
        {
            return newBindPassword.Length > MaxBindPasswordLength
                ? Result<string?>.Invalid("The bind password is too long.")
                : Result<string?>.Success(newBindPassword);
        }

        // The stored password is only used for the saved connection target, never sent to a different server.
        var saved = await configuration.GetSettingsAsync(cancellationToken);
        if (settings.HasDifferentConnectionTarget(saved))
        {
            return Result<string?>.Invalid("Enter the bind password to test a changed server or bind account.");
        }

        var stored = await configuration.GetBindPasswordAsync(cancellationToken);
        return string.IsNullOrEmpty(stored)
            ? Result<string?>.Invalid("Enter the bind password of the service account.")
            : Result<string?>.Success(stored);
    }

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Identity.ManageDirectory, cancellationToken);

    private Task AuditDeniedAsync(string action, string target, IReadOnlyList<string> missing, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry("identity", action, Domain.Observability.AuditOutcome.Denied)
        {
            TargetName = target,
            Message = "Privilege escalation prevented.",
            Details = new { missing },
        }, cancellationToken);
}
