namespace Servora.Web.Components.Pages.Setup;

/// <summary>The pages of the setup wizard, in display order.</summary>
public enum SetupWizardStep
{
    /// <summary>Introduction and data directory.</summary>
    Welcome = 0,

    /// <summary>Database provider, connection fields and connection test.</summary>
    Database = 1,

    /// <summary>Company name, product name, accent color and time zone.</summary>
    Organization = 2,

    /// <summary>Summary of all choices before anything is written.</summary>
    Review = 3,

    /// <summary>Installation progress and the final "go to sign-in" page.</summary>
    Install = 4,
}
