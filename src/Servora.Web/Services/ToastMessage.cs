namespace Servora.Web.Services;

/// <summary>A toast notification shown in the bottom-right corner.</summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="Level">The severity.</param>
/// <param name="Title">The title.</param>
/// <param name="Text">Optional detail text.</param>
public sealed record ToastMessage(Guid Id, ToastLevel Level, string Title, string? Text);
