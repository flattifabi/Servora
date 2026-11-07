namespace Servora.Application.Api;

/// <summary>A scope the current user may grant to an API key.</summary>
/// <param name="Key">The scope key.</param>
/// <param name="Group">The functional group.</param>
/// <param name="Description">The description.</param>
public sealed record GrantableScope(string Key, string Group, string Description);
