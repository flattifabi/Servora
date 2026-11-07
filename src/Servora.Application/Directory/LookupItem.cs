namespace Servora.Application.Directory;

/// <summary>A reference picker entry.</summary>
/// <param name="Id">The record identifier.</param>
/// <param name="Display">The display value.</param>
/// <param name="Secondary">Secondary text such as e-mail, CI class or group type.</param>
public sealed record LookupItem(Guid Id, string Display, string? Secondary = null);
