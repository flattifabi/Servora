# Architecture Overview

```
┌──────────────────────────────── Servora (single process / Windows Service) ────────────────────────────────┐
│ Servora.Web  (host)                                                                                         │
│   Blazor Interactive Server UI · REST /api/v1 · MCP /mcp · auth (cookie, Negotiate, API key) · middleware   │
│        │ calls                                                                                              │
│ Servora.Application  (use cases, services, DTOs, contracts)  ──►  Servora.Domain (entities, enums, rules)   │
│        ▲ implements contracts                                                                               │
│ Servora.Infrastructure  (EF Core context, LDAP/AD, SMTP, HTTP data sources, crypto, background services)    │
│        ▲ provider plug-ins                                                                                  │
│ Servora.Persistence.Sqlite · .SqlServer · .PostgreSql  (EF provider + migrations each)                      │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Request & data flow

1. **UI** components inject application services (scoped per circuit) – never DbContexts.
2. **Application services** check permissions via `ICurrentUser`, create a short-lived `IAppDbContext` from
   `IAppDbContextFactory`, execute the use case and return `Result`/`Result<T>`.
3. **`ServoraDbContext.SaveChangesAsync`** applies audit fields + concurrency version, runs `IRecordSavingHook`s
   (before business rules, priority calculation …), commits and queues `RecordChangedEvent`s.
4. **`RecordEventProcessor`** (background) dispatches events to `IRecordEventHandler`s in fresh scopes as the system
   actor: async business rules, flow triggers, SLA evaluation, notifications.

## Identity & authorization

- Principals: interactive users (cookie; local password, LDAP/AD, Windows Negotiate), API keys (REST/MCP),
  AI agents and system processes (`ActorContext`).
- RBAC: permissions (`Permissions.*` catalog) ← roles ← users directly or via groups (and AD group mappings).
  `PermissionMatcher` supports `*` and `prefix.*`.
- Enforcement: application services (`HasPermissionAsync`), Razor pages (`[RequirePermission]` → dynamic `perm:` policy),
  endpoints (`RequireAuthorization(PermissionPolicy.For(...))`).
- Security stamp revalidation for cookies (per request, cached 30 s) and circuits (every minute).

## Database providers

`IDatabaseProvider` (Key, Configure, TestConnection, Redact) per provider project. `DatabaseSettingsStore` keeps the
active provider + encrypted connection string in `<data dir>/database.json` (or `Servora:Database` configuration for
dev / unattended installs). `DbContextOptionsProvider` rebuilds options when settings change (setup wizard).
Provider differences handled centrally: SQLite stores `DateTimeOffset` as UTC ticks (ordering works).

## Extensibility points

| Need | Extension point |
|------|-----------------|
| New table | Entity + `[RecordTable]` + EF configuration in module folder |
| Before-save logic | `IRecordSavingHook` |
| React to changes | `IRecordEventHandler` |
| Seed data | `IDataSeeder` |
| New permission | constant in `Permissions.<Area>.cs` |
| Sign-in source | `IExternalCredentialValidator` |
| Flow action | `IFlowAction` (T-08) |
| MCP tool | MCP tool registry (T-10) |
| Database provider | `IDatabaseProvider` in a new `Servora.Persistence.*` project |

## Background processing

Hosted services: record event processor (kernel), directory sync (T-02), mail outbox (T-03), SLA evaluator (T-07),
flow engine + scheduler (T-08), log retention/metrics (T-09). All are bounded (batch sizes, concurrency limits) and
cancellation-aware.
