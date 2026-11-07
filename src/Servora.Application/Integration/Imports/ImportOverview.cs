namespace Servora.Application.Integration.Imports;

/// <summary>Key figures of the import area.</summary>
/// <param name="ImportSets">The number of import sets.</param>
/// <param name="Scheduled">The number of scheduled import sets.</param>
/// <param name="Runs7d">Runs in the last 7 days.</param>
/// <param name="RowsImported7d">Inserted plus updated rows in the last 7 days.</param>
/// <param name="RowErrors7d">Failed rows in the last 7 days.</param>
public sealed record ImportOverview(int ImportSets, int Scheduled, int Runs7d, int RowsImported7d, int RowErrors7d);
