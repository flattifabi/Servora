using Servora.Application.Conditions;

namespace Servora.Application.Notifications;

/// <summary>A notification definition as edited in the UI.</summary>
public sealed class NotificationModel
{
    /// <summary>Gets or sets the identifier (empty for new notifications).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the notification is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it fires on insert.</summary>
    public bool OnInsert { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it fires on update.</summary>
    public bool OnUpdate { get; set; }

    /// <summary>Gets or sets a value indicating whether it fires on delete.</summary>
    public bool OnDelete { get; set; }

    /// <summary>Gets or sets the condition (change operators allowed).</summary>
    public ConditionGroup Condition { get; set; } = new();

    /// <summary>Gets or sets the template (null = inline subject and body).</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Gets or sets the inline subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the inline body.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the reference fields whose users / groups receive the notification.</summary>
    public List<string> RecipientFields { get; set; } = [];

    /// <summary>Gets or sets the recipient groups.</summary>
    public List<Guid> RecipientGroupIds { get; set; } = [];

    /// <summary>Gets or sets the recipient users.</summary>
    public List<Guid> RecipientUserIds { get; set; } = [];

    /// <summary>Gets or sets the fixed e-mail addresses (comma separated).</summary>
    public string? RecipientEmails { get; set; }

    /// <summary>Gets or sets a value indicating whether an e-mail is sent.</summary>
    public bool SendEmail { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether in-app notifications are created.</summary>
    public bool SendInApp { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the actor is excluded.</summary>
    public bool ExcludeActor { get; set; } = true;

    /// <summary>Gets or sets the UTC time of the last change (read only).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
