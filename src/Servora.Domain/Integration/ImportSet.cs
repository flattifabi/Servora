using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>
/// An import definition: source (data source endpoint or CSV upload), target table and transform map (source path →
/// target field, coalesce field for insert-or-update). Each execution stages its rows in an <see cref="ImportRun"/>.
/// </summary>
public class ImportSet : AuditableEntity
{
    /// <summary>Gets or sets the unique display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the source type.</summary>
    public ImportSourceType SourceType { get; set; } = ImportSourceType.CsvUpload;

    /// <summary>Gets or sets the data source for <see cref="ImportSourceType.DataSource"/>.</summary>
    public Guid? DataSourceId { get; set; }

    /// <summary>Gets or sets the endpoint key.</summary>
    public string? EndpointKey { get; set; }

    /// <summary>Gets or sets the JSON object of endpoint parameter values.</summary>
    public string? ParametersJson { get; set; }

    /// <summary>Gets or sets the JSON path of the record array in the response (empty = response root).</summary>
    public string? RecordsPath { get; set; }

    /// <summary>Gets or sets the target table.</summary>
    public string TargetTable { get; set; } = string.Empty;

    /// <summary>Gets or sets the target field used to find existing records (insert or update); empty = always insert.</summary>
    public string? CoalesceField { get; set; }

    /// <summary>Gets or sets the JSON array of field maps.</summary>
    public string FieldMapsJson { get; set; } = "[]";

    /// <summary>Gets or sets the cron expression for scheduled data source imports.</summary>
    public string? CronExpression { get; set; }

    /// <summary>Gets or sets the time zone identifier of the cron expression (UTC when empty).</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Gets or sets a value indicating whether the schedule is active.</summary>
    public bool ScheduleEnabled { get; set; }

    /// <summary>Gets or sets the next UTC scheduled run.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Gets or sets the UTC time of the last run.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
}
