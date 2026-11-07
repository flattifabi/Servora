namespace Servora.Application.Cmdb;

/// <summary>Number of configuration items of a class.</summary>
/// <param name="Key">The class key.</param>
/// <param name="Label">The class label.</param>
/// <param name="Icon">The class icon.</param>
/// <param name="Color">The class color.</param>
/// <param name="Count">The number of CIs.</param>
public sealed record CiClassCount(string Key, string Label, string Icon, string Color, int Count);
