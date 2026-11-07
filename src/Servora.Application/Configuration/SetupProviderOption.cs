namespace Servora.Application.Configuration;

/// <summary>A database provider offered by the setup wizard.</summary>
/// <param name="Key">The provider key.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="ExampleConnectionString">An example connection string.</param>
public sealed record SetupProviderOption(string Key, string DisplayName, string ExampleConnectionString);
