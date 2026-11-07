using Servora.Domain.Common;

namespace Servora.Domain.Notifications;

/// <summary>
/// A notification: trigger (table, events, condition), content (template or inline subject / body) and recipients
/// (reference fields, groups, users, fixed addresses). Delivered by e-mail and / or the in-app inbox.
/// </summary>
public class NotificationDefinition : AuditableEntity
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the notification is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the notification fires when a record is inserted.</summary>
    public bool OnInsert { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the notification fires when a record is updated.</summary>
    public bool OnUpdate { get; set; }

    /// <summary>Gets or sets a value indicating whether the notification fires when a record is deleted.</summary>
    public bool OnDelete { get; set; }

    /// <summary>Gets or sets the condition JSON (supports change operators); empty always matches.</summary>
    public string? ConditionJson { get; set; }

    /// <summary>Gets or sets the template; when null the inline <see cref="Subject"/> and <see cref="Body"/> are used.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Gets or sets the inline subject template.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the inline body template.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the reference fields (to users or groups) whose targets receive the notification, as JSON array.</summary>
    public string? RecipientFieldsJson { get; set; }

    /// <summary>Gets or sets fixed recipient groups (all active members) as JSON array of ids.</summary>
    public string? RecipientGroupIdsJson { get; set; }

    /// <summary>Gets or sets fixed recipient users as JSON array of ids.</summary>
    public string? RecipientUserIdsJson { get; set; }

    /// <summary>Gets or sets fixed e-mail addresses (comma separated).</summary>
    public string? RecipientEmails { get; set; }

    /// <summary>Gets or sets a value indicating whether an e-mail is sent.</summary>
    public bool SendEmail { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether an in-app notification is created for recipient users.</summary>
    public bool SendInApp { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the user who caused the change is excluded.</summary>
    public bool ExcludeActor { get; set; } = true;
}
