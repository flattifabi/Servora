# Observability & Analytics (T-09)

This module gives administrators insight into a running Servora instance (logs, audit trail, health, request metrics)
and gives every user with analytics access dashboards and a report builder.

| Area | UI | Permission |
|------|----|------------|
| Log viewer + debug switch | `/admin/logs` | `admin.logs.read` (view, download), `admin.logs.manage` (levels, retention) |
| Audit log viewer | `/admin/audit` | `admin.audit.read` (view, export, verify); retention also needs `admin.settings.manage` |
| System health + metrics | `/admin/health`, `GET /health`, `GET /api/v1/observability/health`, `GET /api/v1/observability/metrics` | `admin.health.read` (details) |
| Executive dashboard + personal pins | `/analytics` | `analytics.read` + read access to the source tables |
| Report builder | `/analytics/reports`, `/analytics/reports/new`, `/analytics/reports/{id}` | `analytics.read` (view/run), `analytics.reports.manage` (create/edit/share/delete) |

Every application service method checks these permissions server-side (`Result.Forbidden()` otherwise); the pages
additionally carry `[RequirePermission]`.

## Logging

* **Provider.** `ServoraLoggerProvider` is added to `builder.Logging` by `ObservabilityWebModule`
  (`AddServoraLogging`). Each entry is redacted once and written to two sinks:
  * `InMemoryLogSink` – ring buffer of the last *N* entries (default 5,000) that backs the log viewer;
  * `JsonFileLogSink` – rolling JSON-lines files `servora-YYYYMMDD[-n].jsonl` in `<data dir>/logs`, written by a
    background writer through a bounded queue. Files roll daily and when the size cap is reached; files older than the
    retention period are deleted (at start-up, every 6 hours and when retention is changed).
* **Correlation id.** `CorrelationIdMiddleware` (inserted through an `IStartupFilter`, `Program.cs` is untouched)
  reuses a safe incoming `X-Correlation-ID` header (`[A-Za-z0-9._:-]{1,64}`) or creates one, sets
  `HttpContext.TraceIdentifier`, echoes the header and opens a logging scope, so every entry and audit event of a request
  shares the id.
* **Redaction.** `SecretRedactor` masks values of sensitive keys (`password`, `pwd`, `secret`, `token`, `api_key`,
  `authorization`, `connection_string`, …) in `key=value`, `key: value` and JSON forms, credentials in URLs,
  `Bearer/Basic/…` authorization values and JWTs – in the message, the exception text and structured properties.
  Structured properties whose *name* is sensitive are always replaced with `***`.

Start-up options (`appsettings.json`, section `Servora:Logging`): `RingBufferCapacity` (5000), `RetentionDays` (14),
`MaxFileSizeMb` (20), `FileQueueCapacity` (20000). Retention and size changed in the UI are persisted as system
settings and win over the configuration.

## Debug switch

`/admin/logs → Log levels` changes the minimum level at runtime – globally (Trace … Critical, or "configuration
default") and per category prefix (e.g. `Microsoft.EntityFrameworkCore` = Debug, `Servora.Automation*`). The change
is applied immediately to all existing loggers (`LogLevelSwitch` reloads `LoggerFilterOptions` through a change token,
no restart), persisted as the system setting `observability.logging.levels`, restored at start-up and audited
(`config / logging.levels.changed`). An optional **auto-revert** (1–1440 minutes) restores the levels that were active
before the debug session; `ObservabilityMaintenanceService` checks every 15 seconds.

The log viewer offers live tail (2 s auto-refresh), filters for minimum level, category, text and correlation id, a
detail drawer with properties and exception, export of the filtered entries (JSON lines) and download of the log
files. Exports and downloads are audited.

## Audit viewer

`/admin/audit` lists the hash-chained audit log (`AuditEvent`) with filters (category, action, actor, outcome, date
range, text), KPIs, a detail drawer and CSV export (`/observability/audit/export`, at most 50,000 rows, formula
injection safe, audited).

**Hash-chain verification.** Each event stores `Sequence` (its chain position, assigned under the writer lock),
`PreviousHash` and `Hash = SHA-256(canonical content | PreviousHash)` (`AuditHasher`). `AuditChainVerifier` walks the
chain in sequence order in bounded batches and reports:

* `HashMismatch` – content changed after writing;
* `BrokenLink` – `PreviousHash` does not match the predecessor (event recomputed or reordered);
* `SequenceGap` / `MissingStart` – events deleted;
* `MissingHash` – an event without hash.

The result (intact or the first issues) is stored and shown on the page and audited.

**Retention.** `observability.audit.retention_days` (0 = keep forever, otherwise 30–3650). Pruning runs in the
background in bounded batches and stores the hash and sequence of the last pruned event as the *chain anchor*, so the
remaining chain still verifies.

## Health & metrics

ASP.NET Core health checks registered by `ObservabilityModule`:

| Check | Unhealthy / Degraded when |
|-------|---------------------------|
| `database` | connection fails / latency > 1 s or setup pending |
| `disk` (data directory drive) | < 500 MB free / < 2 GB, or < 10 % while < 20 GB free |
| `event-queue` (record events) | backlog > 8,000 / > 1,000 |
| `background-services` | a `BackgroundService` crashed / stopped or a heartbeat (`IHeartbeatRegistry`) is overdue |
| `process` | – / sustained CPU > 90 % |

`GET /health` returns `{ status, checkedAt }` anonymously and the full per-check report for principals with
`admin.health.read`. `/admin/health` adds process metrics (CPU, memory, GC, threads, uptime, version), heartbeats and
request metrics with 10 s auto-refresh.

`RequestMetricsMiddleware` aggregates request count, errors (5xx), latency (average, p95 via histogram buckets) per
minute for the last 24 hours and per route template (bounded cardinality) in memory (`RequestMetricsCollector`).

## Analytics dashboard

`IAnalyticsService.GetExecutiveDashboardAsync(periodDays)` computes, from the typed tables that exist at runtime
(table registry + EF model; missing modules become "not installed" widgets, missing permissions "restricted"):

* KPIs: open incidents, MTTR, SLA compliance, automation success rate, active users, sign-ins, configuration items,
  security events (failed / denied operations), each with delta against the previous period and a sparkline;
* charts: incidents opened vs resolved, backlog by priority, MTTR trend, top categories, SLA compliance, changes by
  state, requests by item, automation success, CIs by class, users by source, sign-ins, audit activity.

All queries project only the needed columns, are bounded (`MaxRows`) and aggregate in memory (provider neutral).
Below the executive dashboard every user sees their **pinned reports** (up to 12, normal or wide tiles).

## Report builder

A report (`ReportDefinition`, table `sys_report`) stores: source table (`ITableRegistry`), filter (`ConditionBuilder`
→ `ConditionJson`), group-by field (date fields with day/week/month/quarter/year buckets), aggregate (count, sum,
average, min, max of a numeric field), chart type (bar, horizontal bar, line, donut, table, single metric), maximum groups (rest
folded into "Other"), visibility (private, a role, everyone).

`ReportEngine` runs a specification with the viewer's permissions: it requires read access to the table (table or
module read permission, `TableAccessPolicy`), rejects unknown and `[NotExposed]` fields, applies the condition through
the platform `IConditionEvaluator` (unsupported conditions run unfiltered with a warning), resolves reference and
choice labels and caps the scanned rows. Custom tables go through `IRecordService` when the platform engine provides
it. Only the owner edits or deletes a report (administrators with `*` too); others can save a copy. Saving, sharing
and deleting is audited.

`AnalyticsSeeder` (order 900) adds built-in sample reports (shared with everyone) for every installed table and pins
the first four to the bootstrap administrator's dashboard; it is idempotent per report name, so samples for modules
installed later are added on the next start.

## Data model

| Entity | Table | Notes |
|--------|-------|-------|
| `AuditEvent` (kernel, extended) | `sys_audit_event` | `+ Sequence` (indexed), index on `ActorName` |
| `ReportDefinition` | `sys_report` | auditable entity, configuration (not a record table) |
| `DashboardPin` | `sys_dashboard_pin` | user ↔ report, position, wide flag; unique per user + report |

Settings (system settings, category "Observability"): `observability.logging.levels`, `…retention_days`,
`…max_file_size_mb`, `observability.audit.retention_days`, `…chain_anchor`, `…chain_anchor_sequence`,
`…last_verification`.

## Tests

`tests/Servora.Tests.Unit/Observability/`: debug switch (live level change, prefix overrides, persistence, restore,
auto-revert, authorization), logs on disk and in memory with redaction and correlation id, redaction patterns, file
retention, audit chain tampering detection (modified, recomputed, deleted, pruned), CSV escaping, request metrics
aggregation, disk thresholds, report engine (grouping, references, date buckets, permissions, hidden fields), report
service (save + grouped render, role sharing, owner-only edit, pins), executive dashboard with seed data and
restricted sections, sample report seeder.
