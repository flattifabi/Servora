namespace Servora.Domain.Cmdb;

/// <summary>Technical table names of the CMDB module (in addition to <see cref="Common.TableNames.ConfigurationItem"/>).</summary>
public static class CmdbTables
{
    /// <summary>CI classes.</summary>
    public const string CiClass = "cmdb_class";

    /// <summary>CI relationships.</summary>
    public const string Relationship = "cmdb_rel_ci";
}
