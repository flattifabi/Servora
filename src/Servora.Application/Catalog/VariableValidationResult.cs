namespace Servora.Application.Catalog;

/// <summary>Outcome of validating the answers of an order form.</summary>
/// <param name="Errors">Validation errors keyed by variable name.</param>
/// <param name="Values">The normalized answers keyed by variable name (only for known variables).</param>
public sealed record VariableValidationResult(IReadOnlyDictionary<string, string> Errors, IReadOnlyDictionary<string, string?> Values)
{
    /// <summary>Gets a value indicating whether all answers are valid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Gets a single user-safe message summarizing the errors.</summary>
    public string Summary => Errors.Count == 0 ? string.Empty : "Please correct the order form: " + string.Join(" ", Errors.Values.Take(5));
}
