using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Problem Management module.</summary>
    [Description("Problem Management")]
    public static class Problems
    {
        /// <summary>View problems and known errors.</summary>
        [Description("View problems and known errors.")]
        public const string Read = "problem.read";

        /// <summary>Create problems.</summary>
        [Description("Create problems.")]
        public const string Create = "problem.create";

        /// <summary>Update and resolve problems.</summary>
        [Description("Update and resolve problems.")]
        public const string Write = "problem.write";

        /// <summary>Delete problems.</summary>
        [Description("Delete problems.")]
        public const string Delete = "problem.delete";
    }
}
