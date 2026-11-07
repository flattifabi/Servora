namespace Servora.Installer.Model;

/// <summary>Account the Servora service runs under.</summary>
internal enum ServiceAccountKind
{
    /// <summary>
    /// Virtual service account <c>NT SERVICE\Servora</c> (default, least privilege). Accesses the network as the
    /// computer account, like LocalSystem, so Windows authentication to SQL Server and Active Directory still works.
    /// </summary>
    Virtual,

    /// <summary>The built-in <c>LocalSystem</c> account (full local privileges).</summary>
    LocalSystem,

    /// <summary>The built-in <c>NT AUTHORITY\NetworkService</c> account.</summary>
    NetworkService,
}
