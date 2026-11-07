namespace Servora.Installer.Model;

/// <summary>Resolved directories of one Servora installation.</summary>
/// <param name="InstallDirectory">Directory with the program files (replaced on update).</param>
/// <param name="DataDirectory">Data directory (database settings, SQLite DB, keys, logs, attachments, instance settings). Never replaced.</param>
/// <param name="BackupDirectory">Root directory of the binary backups (<c>%ProgramData%\Servora\backups</c>).</param>
internal sealed record InstallerLayout(string InstallDirectory, string DataDirectory, string BackupDirectory)
{
    /// <summary>Gets the full path of the service executable.</summary>
    public string ExecutablePath => Path.Combine(InstallDirectory, ProductInfo.ExecutableName);

    /// <summary>Gets the full path of the instance <c>appsettings.json</c> in the data directory.</summary>
    public string InstanceSettingsPath => Path.Combine(DataDirectory, ProductInfo.InstanceSettingsFileName);
}
