namespace Servora.Application.Notifications;

/// <summary>An in-app notification shown in the inbox.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Body">The body (plain text).</param>
/// <param name="Link">The relative link.</param>
/// <param name="CreatedAt">The UTC creation time.</param>
/// <param name="IsRead">Whether the notification was read.</param>
public sealed record InboxItem(Guid Id, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool IsRead);
