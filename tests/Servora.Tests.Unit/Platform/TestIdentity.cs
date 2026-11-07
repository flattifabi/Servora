namespace Servora.Tests.Unit.Platform;

/// <summary>Mutable identity (user id and granted permissions) shared by a test host.</summary>
public sealed class TestIdentity
{
    /// <summary>Gets or sets the user id of the acting user.</summary>
    public Guid? UserId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = "Test User";

    /// <summary>Gets the granted permissions (supports <c>*</c> and <c>prefix.*</c>).</summary>
    public HashSet<string> Permissions { get; } = new(StringComparer.Ordinal) { "*" };

    /// <summary>Replaces the granted permissions.</summary>
    /// <param name="permissions">The permissions.</param>
    public void Grant(params string[] permissions)
    {
        Permissions.Clear();
        foreach (var permission in permissions)
        {
            Permissions.Add(permission);
        }
    }
}
