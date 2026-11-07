using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Integrations module.</summary>
    [Description("Integrations")]
    public static class Integration
    {
        /// <summary>View data sources and execution history.</summary>
        [Description("View data sources and execution history.")]
        public const string ReadDataSources = "integration.datasources.read";

        /// <summary>Create, edit and delete data sources and their endpoints.</summary>
        [Description("Create, edit and delete data sources and their endpoints.")]
        public const string ManageDataSources = "integration.datasources.manage";

        /// <summary>Execute approved data source endpoints.</summary>
        [Description("Execute approved data source endpoints.")]
        public const string ExecuteDataSources = "integration.datasources.execute";

        /// <summary>Manage import sets and transform maps.</summary>
        [Description("Manage import sets and transform maps.")]
        public const string ManageImports = "integration.imports.manage";
    }
}
