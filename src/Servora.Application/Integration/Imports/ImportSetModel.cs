using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>Editable definition of an import set (read and save model).</summary>
public sealed class ImportSetModel
{
    /// <summary>Gets or sets the identifier (empty for new sets).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the source type.</summary>
    public ImportSourceType SourceType { get; set; } = ImportSourceType.CsvUpload;

    /// <summary>Gets or sets the data source.</summary>
    public Guid? DataSourceId { get; set; }

    /// <summary>Gets or sets the endpoint key.</summary>
    public string? EndpointKey { get; set; }

    /// <summary>Gets or sets the endpoint parameter values.</summary>
    public Dictionary<string, string> Parameters { get; set; } = [];

    /// <summary>Gets or sets the JSON path of the record array.</summary>
    public string? RecordsPath { get; set; }

    /// <summary>Gets or sets the target table.</summary>
    public string TargetTable { get; set; } = string.Empty;

    /// <summary>Gets or sets the coalesce field.</summary>
    public string? CoalesceField { get; set; }

    /// <summary>Gets or sets the field maps.</summary>
    public List<ImportFieldMap> FieldMaps { get; set; } = [];

    /// <summary>Gets or sets the cron expression.</summary>
    public string? CronExpression { get; set; }

    /// <summary>Gets or sets the time zone.</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Gets or sets a value indicating whether the schedule is active.</summary>
    public bool ScheduleEnabled { get; set; }

    /// <summary>Gets or sets the next scheduled run (read only).</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Gets or sets the last run (read only).</summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>Gets or sets the concurrency token.</summary>
    public long ConcurrencyVersion { get; set; }
}
