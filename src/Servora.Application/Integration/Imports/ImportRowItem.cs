using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>A staged import row with its transform result.</summary>
/// <param name="Id">The row.</param>
/// <param name="RowNumber">The source row number.</param>
/// <param name="Status">The status.</param>
/// <param name="DataJson">The staged data.</param>
/// <param name="TargetRecordId">The written record.</param>
/// <param name="Message">The result message.</param>
public sealed record ImportRowItem(Guid Id, int RowNumber, ImportRowStatus Status, string DataJson, Guid? TargetRecordId, string? Message);
