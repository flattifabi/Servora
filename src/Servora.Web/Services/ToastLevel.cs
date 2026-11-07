namespace Servora.Web.Services;

/// <summary>Visual severity of a toast notification.</summary>
public enum ToastLevel
{
    /// <summary>Neutral information.</summary>
    Info = 0,

    /// <summary>Successful operation.</summary>
    Success = 1,

    /// <summary>Warning.</summary>
    Warning = 2,

    /// <summary>Error.</summary>
    Error = 3,
}
