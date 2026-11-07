using Servora.Application.Tables;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="ITableRegistry"/> with an incident-like and a contact table.</summary>
internal sealed class FakeTableRegistry : ITableRegistry
{
    /// <summary>The incident-like test table.</summary>
    public static readonly TableDefinition Incident = new()
    {
        Name = "incident",
        Label = "Incident",
        PluralLabel = "Incidents",
        DisplayField = "Number",
        Fields =
        [
            new FieldDefinition { Name = "Number", Label = "Number", Type = FieldType.String, IsReadOnly = true },
            new FieldDefinition { Name = "ShortDescription", Label = "Short description", Type = FieldType.String },
            new FieldDefinition { Name = "Priority", Label = "Priority", Type = FieldType.Integer },
            new FieldDefinition { Name = "State", Label = "State", Type = FieldType.String },
            new FieldDefinition { Name = "CallerEmail", Label = "Caller e-mail", Type = FieldType.Email },
            new FieldDefinition { Name = "AssignmentGroup", Label = "Assignment group", Type = FieldType.String },
        ],
    };

    /// <summary>The contact test table used by imports.</summary>
    public static readonly TableDefinition Contact = new()
    {
        Name = "contact",
        Label = "Contact",
        PluralLabel = "Contacts",
        DisplayField = "Name",
        Fields =
        [
            new FieldDefinition { Name = "Email", Label = "E-mail", Type = FieldType.Email },
            new FieldDefinition { Name = "Name", Label = "Name", Type = FieldType.String },
            new FieldDefinition { Name = "City", Label = "City", Type = FieldType.String },
        ],
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<TableDefinition>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TableDefinition>>([Incident, Contact]);

    /// <inheritdoc />
    public Task<TableDefinition?> GetTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new[] { Incident, Contact }.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc />
    public string? GetTableName(Type entityType) => null;

    /// <inheritdoc />
    public Task InvalidateAsync() => Task.CompletedTask;
}
