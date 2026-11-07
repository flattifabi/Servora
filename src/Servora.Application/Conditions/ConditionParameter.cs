namespace Servora.Application.Conditions;

/// <summary>
/// Holder for a condition value inside a generated LINQ expression. EF Core turns member access on a constant holder
/// into a SQL parameter, so condition values are never inlined into SQL text and query plans can be reused.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="value">The value.</param>
internal sealed class ConditionParameter<T>(T value)
{
    /// <summary>Gets the value.</summary>
    public T Value { get; } = value;
}
