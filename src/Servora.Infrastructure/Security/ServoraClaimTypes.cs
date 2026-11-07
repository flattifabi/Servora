namespace Servora.Infrastructure.Security;

/// <summary>Custom claim types issued by Servora authentication handlers.</summary>
public static class ServoraClaimTypes
{
    /// <summary>Display name of the user.</summary>
    public const string DisplayName = "servora:display_name";

    /// <summary>Security stamp of the user at sign-in (cookie validation).</summary>
    public const string SecurityStamp = "servora:security_stamp";

    /// <summary>Authentication source: <c>local</c>, <c>directory</c>, <c>windows</c>.</summary>
    public const string AuthSource = "servora:auth_source";

    /// <summary>Actor type: <c>user</c>, <c>apikey</c>, <c>agent</c>.</summary>
    public const string ActorType = "servora:actor_type";

    /// <summary>API key identifier for API key principals.</summary>
    public const string ApiKeyId = "servora:api_key_id";

    /// <summary>Granted permission scope (one claim per key) for API key and agent principals.</summary>
    public const string Permission = "servora:perm";

    /// <summary>Set to <c>true</c> while the user must change the password before using the application.</summary>
    public const string MustChangePassword = "servora:must_change_password";
}
