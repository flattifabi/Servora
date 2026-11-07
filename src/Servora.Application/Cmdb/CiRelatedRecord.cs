namespace Servora.Application.Cmdb;

/// <summary>A record of another module that references a configuration item (incident, problem, change, article, asset).</summary>
/// <param name="Table">The technical table name.</param>
/// <param name="TableLabel">The table label.</param>
/// <param name="Icon">The table icon.</param>
/// <param name="Id">The record identifier.</param>
/// <param name="Number">The record number or display value.</param>
/// <param name="Title">The short description / title.</param>
/// <param name="State">The state display value.</param>
/// <param name="Url">The relative UI route of the record.</param>
public sealed record CiRelatedRecord(string Table, string TableLabel, string Icon, Guid Id, string Number, string Title, string? State, string Url);
