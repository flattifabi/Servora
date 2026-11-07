using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Servora.Application.Identity.Ldap;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// <see cref="IDirectoryClient"/> based on <c>System.DirectoryServices.Protocols</c> (LDAP v3, simple bind).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Transport security is applied exactly as configured: LDAPS uses TLS from the first byte, StartTLS fails the
/// operation when the upgrade fails – there is never a silent fallback to plain LDAP.</item>
/// <item>Searches always bind with the configured service account; anonymous binds are refused.</item>
/// <item>User passwords are verified by binding with the distinguished name found by the service search, never with
/// the typed login name, and empty passwords are rejected (an empty simple bind is an unauthenticated bind).</item>
/// <item>All filter values are escaped (RFC 4515) and every operation is bounded by the configured timeout and size limits.</item>
/// </list>
/// </remarks>
/// <param name="logger">The logger.</param>
public sealed class LdapDirectoryClient(ILogger<LdapDirectoryClient> logger) : IDirectoryClient
{
    private const int InvalidCredentialsCode = 49;
    private const int PageSize = 500;
    private const int MaxNestedGroups = 1000;
    private const int AccountDisableFlag = 0x2;
    private const string NestedMembershipRule = "1.2.840.113556.1.4.1941";

    /// <inheritdoc />
    public Task<DirectoryTestResult> TestConnectionAsync(DirectorySettings settings, string? bindPassword, CancellationToken cancellationToken = default) =>
        Task.Run(() => TestConnection(settings, bindPassword), cancellationToken);

    /// <inheritdoc />
    public Task<DirectoryLookupResult> FindUserAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, CancellationToken cancellationToken = default) =>
        Task.Run(() => Execute(settings, bindPassword, connection => FindUser(connection, settings, login)), cancellationToken);

    /// <inheritdoc />
    public Task<DirectoryLookupResult> AuthenticateAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(new DirectoryLookupResult(DirectoryLookupOutcome.InvalidPassword, Message: "Empty passwords are not accepted."));
        }

        return Task.Run(() => Authenticate(settings, bindPassword, login, password), cancellationToken);
    }

    /// <inheritdoc />
    public Task<DirectorySearchResult> SearchUsersAsync(DirectorySettings settings, string? bindPassword, int maxEntries, CancellationToken cancellationToken = default) =>
        Task.Run(() => SearchUsers(settings, bindPassword, Math.Clamp(maxEntries, 1, 100_000), cancellationToken), cancellationToken);

    private static string SecurityLabel(LdapSecurityMode mode) => mode switch
    {
        LdapSecurityMode.Ldaps => "LDAPS",
        LdapSecurityMode.StartTls => "StartTLS",
        _ => "plain LDAP (unencrypted)",
    };

    private static string[] UserAttributes(DirectorySettings s)
    {
        var attributes = new List<string>
        {
            s.UserNameAttribute, s.DisplayNameAttribute, s.FirstNameAttribute, s.LastNameAttribute, s.EmailAttribute,
            s.PhoneAttribute, s.TitleAttribute, s.DepartmentAttribute, s.ExternalIdAttribute, s.MemberOfAttribute,
            "objectSid", "userAccountControl",
        };
        if (!string.IsNullOrWhiteSpace(s.UpnAttribute))
        {
            attributes.Add(s.UpnAttribute!);
        }

        return attributes.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? ReadString(SearchResultEntry entry, string? attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute) || entry.Attributes[attribute] is not { Count: > 0 } values)
        {
            return null;
        }

        var value = values[0] switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static byte[]? ReadBinary(SearchResultEntry entry, string attribute)
    {
        if (entry.Attributes[attribute] is not { Count: > 0 } values)
        {
            return null;
        }

        return values.GetValues(typeof(byte[])).OfType<byte[]>().FirstOrDefault();
    }

    private static List<string> ReadStrings(SearchResultEntry entry, string attribute)
    {
        if (entry.Attributes[attribute] is not { Count: > 0 } values)
        {
            return [];
        }

        return values.GetValues(typeof(string)).OfType<string>().Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
    }

    private static string? ReadExternalId(SearchResultEntry entry, string attribute)
    {
        if (string.Equals(attribute, "objectGUID", StringComparison.OrdinalIgnoreCase))
        {
            return ReadBinary(entry, attribute) is { Length: 16 } bytes ? new Guid(bytes).ToString("D") : null;
        }

        return ReadString(entry, attribute);
    }

    private static DirectoryUserEntry Map(SearchResultEntry entry, DirectorySettings s, IReadOnlyList<string>? groups = null)
    {
        var sidBytes = ReadBinary(entry, "objectSid");
        var disabled = int.TryParse(ReadString(entry, "userAccountControl"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var uac) && (uac & AccountDisableFlag) != 0;
        return new DirectoryUserEntry(
            entry.DistinguishedName,
            ReadString(entry, s.UserNameAttribute) ?? string.Empty,
            ReadString(entry, s.DisplayNameAttribute),
            ReadString(entry, s.FirstNameAttribute),
            ReadString(entry, s.LastNameAttribute),
            ReadString(entry, s.EmailAttribute),
            ReadString(entry, s.PhoneAttribute),
            ReadString(entry, s.TitleAttribute),
            ReadString(entry, s.DepartmentAttribute),
            ReadExternalId(entry, s.ExternalIdAttribute),
            sidBytes is null ? null : SidFormatter.Format(sidBytes),
            disabled,
            groups ?? ReadStrings(entry, s.MemberOfAttribute));
    }

    private static string Describe(Exception exception) => exception switch
    {
        LdapException ldap when ldap.ErrorCode == InvalidCredentialsCode => "The directory rejected the credentials (invalid credentials).",
        LdapException ldap when ldap.ErrorCode == 81 => "The directory server is unavailable (connection failed or TLS handshake rejected).",
        LdapException ldap when ldap.ErrorCode == 85 => "The directory operation timed out.",
        LdapException ldap => $"LDAP error {ldap.ErrorCode}: {ldap.Message}",
        TlsOperationException => "The StartTLS upgrade failed. The connection was not used (no fallback to plain LDAP).",
        DirectoryOperationException op => $"Directory operation failed: {op.Response?.ResultCode} {op.Message}",
        PlatformNotSupportedException => "This option is not supported on the server's operating system.",
        _ => exception.Message,
    };

    private LdapConnection Connect(DirectorySettings s, List<string>? steps)
    {
        var identifier = new LdapDirectoryIdentifier(s.Host.Trim(), s.Port, fullyQualifiedDnsHostName: true, connectionless: false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            AutoBind = false,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 1, 120)),
        };

        try
        {
            var options = connection.SessionOptions;
            options.ProtocolVersion = 3;
            options.ReferralChasing = ReferralChasingOptions.None;
            if (s.Security == LdapSecurityMode.Ldaps)
            {
                options.SecureSocketLayer = true;
            }

            if (!s.ValidateCertificate && s.Security != LdapSecurityMode.None)
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Disabling certificate validation is only supported on Windows.");
                }

                options.VerifyServerCertificate = (_, _) => true;
                steps?.Add("Warning: server certificate validation is disabled.");
            }

            if (s.Security == LdapSecurityMode.StartTls)
            {
                // Throws when the server does not support or rejects StartTLS: the connection is then discarded.
                options.StartTransportLayerSecurity(null);
                steps?.Add("StartTLS negotiated.");
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void BindServiceAccount(LdapConnection connection, DirectorySettings s, string? bindPassword)
    {
        if (string.IsNullOrWhiteSpace(s.BindDn) || string.IsNullOrEmpty(bindPassword))
        {
            throw new InvalidOperationException("A service account and password are required. Anonymous binds are not supported.");
        }

        connection.Bind(new NetworkCredential(s.BindDn.Trim(), bindPassword));
    }

    private DirectoryTestResult TestConnection(DirectorySettings settings, string? bindPassword)
    {
        var stopwatch = Stopwatch.StartNew();
        var steps = new List<string> { $"Connecting to {settings.Host}:{settings.Port} using {SecurityLabel(settings.Security)}." };
        try
        {
            using var connection = Connect(settings, steps);
            BindServiceAccount(connection, settings, bindPassword);
            steps.Add($"Connected{(settings.Security == LdapSecurityMode.Ldaps ? " over TLS" : string.Empty)} and bound as the service account.");

            var request = new SearchRequest(settings.BaseDn, "(objectClass=*)", SearchScope.Base, "distinguishedName")
            {
                SizeLimit = 1,
                TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            };
            var response = (SearchResponse)connection.SendRequest(request);
            steps.Add(response.Entries.Count > 0 ? $"Base DN '{settings.BaseDn}' is readable." : $"Base DN '{settings.BaseDn}' returned no entry.");

            var users = new SearchRequest(settings.EffectiveUserSearchBase, settings.UserFilter, SearchScope.Subtree, settings.UserNameAttribute)
            {
                SizeLimit = 1,
                TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            };
            try
            {
                var userResponse = (SearchResponse)connection.SendRequest(users);
                steps.Add(userResponse.Entries.Count > 0 ? "The user filter matches at least one user." : "Warning: the user filter matched no users.");
            }
            catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.SizeLimitExceeded)
            {
                steps.Add("The user filter matches users.");
            }

            if (settings.Security == LdapSecurityMode.None)
            {
                steps.Add("Warning: the connection is not encrypted. Credentials are transmitted in clear text.");
            }

            return new DirectoryTestResult(true, "Connection successful.", steps, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException or InvalidOperationException or PlatformNotSupportedException or ArgumentException)
        {
            logger.LogInformation("Directory connection test to {Host}:{Port} failed: {Error}", settings.Host, settings.Port, Describe(ex));
            steps.Add(Describe(ex));
            return new DirectoryTestResult(false, Describe(ex), steps, stopwatch.ElapsedMilliseconds);
        }
    }

    private DirectoryLookupResult Execute(DirectorySettings settings, string? bindPassword, Func<LdapConnection, DirectoryLookupResult> operation)
    {
        if (!settings.IsConnectionConfigured)
        {
            return new DirectoryLookupResult(DirectoryLookupOutcome.ConfigurationError, Message: "The directory connection is not configured.");
        }

        try
        {
            using var connection = Connect(settings, null);
            BindServiceAccount(connection, settings, bindPassword);
            return operation(connection);
        }
        catch (InvalidOperationException ex)
        {
            return new DirectoryLookupResult(DirectoryLookupOutcome.ConfigurationError, Message: ex.Message);
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException or PlatformNotSupportedException)
        {
            logger.LogWarning("Directory operation against {Host}:{Port} failed: {Error}", settings.Host, settings.Port, Describe(ex));
            return new DirectoryLookupResult(DirectoryLookupOutcome.Unavailable, Message: Describe(ex));
        }
    }

    private DirectoryLookupResult FindUser(LdapConnection connection, DirectorySettings settings, DirectoryLoginName login)
    {
        var filter = LdapFilter.BuildUserLookup(settings, login);
        var request = new SearchRequest(settings.EffectiveUserSearchBase, filter, SearchScope.Subtree, UserAttributes(settings))
        {
            SizeLimit = 2,
            TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        };

        SearchResponse response;
        try
        {
            response = (SearchResponse)connection.SendRequest(request);
        }
        catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.SizeLimitExceeded)
        {
            return new DirectoryLookupResult(DirectoryLookupOutcome.UserNotFound, Message: "The login name is ambiguous (several directory users match).");
        }

        if (response.Entries.Count != 1)
        {
            return new DirectoryLookupResult(
                DirectoryLookupOutcome.UserNotFound,
                Message: response.Entries.Count == 0 ? "No directory user matches this login name." : "The login name is ambiguous (several directory users match).");
        }

        var entry = response.Entries[0];
        var groups = settings.ResolveNestedGroups ? ResolveNestedGroups(connection, settings, entry.DistinguishedName) : null;
        var mapped = Map(entry, settings, groups);
        return string.IsNullOrWhiteSpace(mapped.UserName)
            ? new DirectoryLookupResult(DirectoryLookupOutcome.UserNotFound, Message: $"The directory entry has no '{settings.UserNameAttribute}' attribute.")
            : new DirectoryLookupResult(DirectoryLookupOutcome.Success, mapped);
    }

    private List<string> ResolveNestedGroups(LdapConnection connection, DirectorySettings settings, string userDn)
    {
        var filter = LdapFilter.And(settings.GroupFilter.Trim(), $"(member:{NestedMembershipRule}:={LdapFilter.Escape(userDn)})");
        var request = new SearchRequest(settings.EffectiveGroupSearchBase, filter, SearchScope.Subtree, "distinguishedName")
        {
            SizeLimit = MaxNestedGroups,
            TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        };
        try
        {
            var response = (SearchResponse)connection.SendRequest(request);
            return response.Entries.Cast<SearchResultEntry>().Select(e => e.DistinguishedName).ToList();
        }
        catch (DirectoryOperationException ex)
        {
            logger.LogWarning("Nested group resolution failed for a directory user: {Error}", Describe(ex));
            return [];
        }
    }

    private DirectoryLookupResult Authenticate(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, string password)
    {
        var found = Execute(settings, bindPassword, connection => FindUser(connection, settings, login));
        if (!found.Succeeded)
        {
            return found;
        }

        try
        {
            using var userConnection = Connect(settings, null);
            userConnection.Bind(new NetworkCredential(found.Entry!.DistinguishedName, password));
            return found;
        }
        catch (LdapException ex) when (ex.ErrorCode == InvalidCredentialsCode)
        {
            return new DirectoryLookupResult(DirectoryLookupOutcome.InvalidPassword, found.Entry, "The directory rejected the password.");
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException or PlatformNotSupportedException)
        {
            logger.LogWarning("Directory user bind against {Host}:{Port} failed: {Error}", settings.Host, settings.Port, Describe(ex));
            return new DirectoryLookupResult(DirectoryLookupOutcome.Unavailable, Message: Describe(ex));
        }
    }

    private DirectorySearchResult SearchUsers(DirectorySettings settings, string? bindPassword, int maxEntries, CancellationToken cancellationToken)
    {
        if (!settings.IsConnectionConfigured)
        {
            return DirectorySearchResult.Failed("The directory connection is not configured.");
        }

        try
        {
            using var connection = Connect(settings, null);
            BindServiceAccount(connection, settings, bindPassword);
            var users = new List<DirectoryUserEntry>();
            var request = new SearchRequest(settings.EffectiveUserSearchBase, settings.UserFilter.Trim(), SearchScope.Subtree, UserAttributes(settings))
            {
                TimeLimit = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            };
            var paging = new PageResultRequestControl(PageSize);
            request.Controls.Add(paging);
            var truncated = false;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = (SearchResponse)connection.SendRequest(request);
                foreach (SearchResultEntry entry in response.Entries)
                {
                    if (users.Count >= maxEntries)
                    {
                        truncated = true;
                        break;
                    }

                    var groups = settings.ResolveNestedGroups ? ResolveNestedGroups(connection, settings, entry.DistinguishedName) : null;
                    var mapped = Map(entry, settings, groups);
                    if (!string.IsNullOrWhiteSpace(mapped.UserName))
                    {
                        users.Add(mapped);
                    }
                }

                var cookie = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault()?.Cookie;
                if (truncated || cookie is null || cookie.Length == 0)
                {
                    break;
                }

                paging.Cookie = cookie;
            }

            return new DirectorySearchResult(true, users, truncated);
        }
        catch (InvalidOperationException ex)
        {
            return DirectorySearchResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException or PlatformNotSupportedException)
        {
            logger.LogWarning("Directory user search against {Host}:{Port} failed: {Error}", settings.Host, settings.Port, Describe(ex));
            return DirectorySearchResult.Failed(Describe(ex));
        }
    }
}
