namespace Servora.Application.Configuration;

/// <summary>Key figures of the secret store.</summary>
/// <param name="Total">The number of stored secrets.</param>
/// <param name="System">Secrets used by built-in features.</param>
/// <param name="RotatedLast90Days">Secrets whose value was set within the last 90 days.</param>
/// <param name="Stale">Secrets whose value is older than <see cref="SecretNameRules.StaleAfterDays"/> days.</param>
public sealed record SecretStatistics(int Total, int System, int RotatedLast90Days, int Stale);
