using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Knowledge Management module.</summary>
    [Description("Knowledge Management")]
    public static class Knowledge
    {
        /// <summary>Read published knowledge articles.</summary>
        [Description("Read published knowledge articles.")]
        public const string Read = "knowledge.read";

        /// <summary>Author and edit knowledge articles.</summary>
        [Description("Author and edit knowledge articles.")]
        public const string Write = "knowledge.write";

        /// <summary>Review, publish and retire knowledge articles.</summary>
        [Description("Review, publish and retire knowledge articles.")]
        public const string Publish = "knowledge.publish";

        /// <summary>Manage knowledge bases and categories.</summary>
        [Description("Manage knowledge bases and categories.")]
        public const string Manage = "knowledge.manage";
    }
}
