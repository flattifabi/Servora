namespace Servora.Application.Configuration;

/// <summary>The steps of the installation performed at the end of the setup wizard.</summary>
public enum SetupStep
{
    /// <summary>Verify that the database is reachable.</summary>
    ValidateConnection = 0,

    /// <summary>Create (or migrate) the schema.</summary>
    CreateSchema = 1,

    /// <summary>Store the encrypted database configuration and activate it.</summary>
    SaveConfiguration = 2,

    /// <summary>Seed roles, the bootstrap administrator and module reference data.</summary>
    SeedData = 3,

    /// <summary>Apply the organisation settings (branding, time zone).</summary>
    ApplyOrganization = 4,

    /// <summary>Remove the setup token and finish.</summary>
    Finalize = 5,
}
