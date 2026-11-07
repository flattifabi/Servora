namespace Servora.Domain.Changes;

/// <summary>Risk level of a change request, calculated by the risk assessment.</summary>
public enum ChangeRisk
{
    /// <summary>Low risk.</summary>
    Low = 0,

    /// <summary>Moderate risk.</summary>
    Moderate = 1,

    /// <summary>High risk.</summary>
    High = 2,

    /// <summary>Very high risk.</summary>
    VeryHigh = 3,
}
