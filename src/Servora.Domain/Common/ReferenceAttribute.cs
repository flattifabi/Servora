namespace Servora.Domain.Common;

/// <summary>
/// Marks a <see cref="Guid"/> property as a reference to a record of another table. The table registry uses it
/// to render reference pickers, resolve display values and build dot-walk conditions.
/// </summary>
/// <param name="table">The technical name of the referenced table, for example <c>sys_user</c>.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ReferenceAttribute(string table) : Attribute
{
    /// <summary>Gets the technical name of the referenced table.</summary>
    public string Table { get; } = table;
}
