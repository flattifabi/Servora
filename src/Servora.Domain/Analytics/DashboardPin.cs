using Servora.Domain.Common;

namespace Servora.Domain.Analytics;

/// <summary>A report pinned to the personal analytics dashboard of a user.</summary>
public class DashboardPin : Entity
{
    /// <summary>Gets or sets the user owning the personal dashboard.</summary>
    [Reference(TableNames.User)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the pinned report.</summary>
    public Guid ReportId { get; set; }

    /// <summary>Gets or sets the display position (ascending).</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets a value indicating whether the tile spans the full dashboard width.</summary>
    public bool Wide { get; set; }

    /// <summary>Gets or sets the UTC time the report was pinned.</summary>
    public DateTimeOffset PinnedAt { get; set; }
}
