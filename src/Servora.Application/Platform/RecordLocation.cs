namespace Servora.Application.Platform;

/// <summary>The location of a record resolved from its number.</summary>
/// <param name="TableName">The technical table name.</param>
/// <param name="Id">The record identifier.</param>
/// <param name="Url">The relative UI route of the record.</param>
public sealed record RecordLocation(string TableName, Guid Id, string Url);
