namespace Servora.Domain.Itsm;

/// <summary>Business priority of task records. Lower value means more urgent.</summary>
public enum Priority
{
    /// <summary>P1 – critical, business stopped.</summary>
    Critical = 1,

    /// <summary>P2 – high.</summary>
    High = 2,

    /// <summary>P3 – moderate.</summary>
    Moderate = 3,

    /// <summary>P4 – low.</summary>
    Low = 4,

    /// <summary>P5 – planning / informational.</summary>
    Planning = 5,
}
