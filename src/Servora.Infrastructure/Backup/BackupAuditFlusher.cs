using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>Writes audit events queued in <c>backup.json</c> (startup restore, pre-migration backups, command line) to the audit log.</summary>
/// <param name="scopes">The scope factory.</param>
/// <param name="settings">The settings store.</param>
public sealed class BackupAuditFlusher(IServiceScopeFactory scopes, BackupSettingsStore settings)
{
    /// <summary>Flushes the queued audit events.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of events written.</returns>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        var pending = settings.GetState().PendingAudits;
        if (pending.Count == 0)
        {
            return 0;
        }

        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Backup"));
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        foreach (var item in pending)
        {
            await audit.WriteAsync(
                new AuditEntry("backup", item.Action, item.Outcome)
                {
                    TargetType = "backup",
                    TargetId = item.BackupId,
                    TargetName = item.BackupId,
                    Message = item.Message,
                    ActorNameOverride = item.Actor,
                    Details = new { occurredAt = item.At },
                },
                cancellationToken);
        }

        settings.UpdateState(s => s.PendingAudits.RemoveAll(p => pending.Any(w => w.At == p.At && w.Action == p.Action && w.BackupId == p.BackupId)));
        return pending.Count;
    }
}
