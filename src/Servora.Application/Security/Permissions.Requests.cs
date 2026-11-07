using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Service Catalog &amp; Requests module.</summary>
    [Description("Service Catalog & Requests")]
    public static class Requests
    {
        /// <summary>View requests, requested items and catalog tasks.</summary>
        [Description("View requests, requested items and catalog tasks.")]
        public const string Read = "request.read";

        /// <summary>Fulfil requests and work on catalog tasks.</summary>
        [Description("Fulfil requests and work on catalog tasks.")]
        public const string Write = "request.write";

        /// <summary>Manage catalog categories, items and variables.</summary>
        [Description("Manage catalog categories, items and variables.")]
        public const string ManageCatalog = "catalog.manage";
    }
}
