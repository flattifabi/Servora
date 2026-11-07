namespace Servora.Domain.Changes;

/// <summary>Technical table names and number prefixes of the change management module.</summary>
public static class ChangeTables
{
    /// <summary>Change tasks.</summary>
    public const string ChangeTask = "change_task";

    /// <summary>Number prefix of change requests.</summary>
    public const string ChangeRequestPrefix = "CHG";

    /// <summary>Number prefix of change tasks.</summary>
    public const string ChangeTaskPrefix = "CTASK";

    /// <summary>Name of the default change advisory board approval group.</summary>
    public const string ChangeAdvisoryBoardGroup = "Change Advisory Board";
}
