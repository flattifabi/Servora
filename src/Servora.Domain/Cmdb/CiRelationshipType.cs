namespace Servora.Domain.Cmdb;

/// <summary>
/// Type of a CI relationship. The relationship always reads "parent &lt;type&gt; child", for example
/// "CRM Application <i>runs on</i> app-srv-01". Parents are upstream (consumers), children downstream (providers).
/// </summary>
public enum CiRelationshipType
{
    /// <summary>Parent depends on child (reverse: used by).</summary>
    DependsOn = 1,

    /// <summary>Parent runs on child (reverse: runs).</summary>
    RunsOn = 2,

    /// <summary>Parent is hosted on child (reverse: hosts).</summary>
    HostedOn = 3,

    /// <summary>Parent connects to child (reverse: connected by).</summary>
    ConnectsTo = 4,

    /// <summary>Parent contains child (reverse: contained by).</summary>
    Contains = 5,
}
