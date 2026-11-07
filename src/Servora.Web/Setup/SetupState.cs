namespace Servora.Web.Setup;

/// <summary>
/// Process-wide flag that keeps the setup wizard reachable while it finishes (database saved, schema being migrated,
/// summary page shown). Set and cleared by the setup wizard.
/// </summary>
public static class SetupState
{
    private static int s_inProgress;

    /// <summary>Gets or sets a value indicating whether the setup wizard is currently completing.</summary>
    public static bool IsInProgress
    {
        get => Volatile.Read(ref s_inProgress) == 1;
        set => Volatile.Write(ref s_inProgress, value ? 1 : 0);
    }
}
