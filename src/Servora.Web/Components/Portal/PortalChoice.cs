namespace Servora.Web.Components.Portal;

/// <summary>A selectable option of a portal form (for example the urgency cards of "Report an issue").</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Title">The label.</param>
/// <param name="Text">An optional explanation.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Tone">An optional tone (<c>danger</c>, <c>warning</c>, <c>info</c>, ...).</param>
public sealed record PortalChoice<T>(T Value, string Title, string? Text, string Icon, string? Tone);
