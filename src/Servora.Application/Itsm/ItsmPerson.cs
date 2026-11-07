namespace Servora.Application.Itsm;

/// <summary>Contact card of a person shown on task forms (caller, assignee).</summary>
/// <param name="Id">The user identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Email">The e-mail address.</param>
/// <param name="Phone">The phone number.</param>
/// <param name="Title">The job title.</param>
/// <param name="Department">The department.</param>
/// <param name="Location">The location.</param>
public sealed record ItsmPerson(Guid Id, string DisplayName, string? Email, string? Phone, string? Title, string? Department, string? Location);
