# Automation & Integration (T-08)

Flow Designer and flow engine, scheduler, REST data sources with SSRF protection and import sets. Everything is
configured in the UI; no code, scripts or arbitrary URLs are ever executed.

| Area | Route | Permission |
|------|-------|------------|
| Flow list / Flow Designer | `/automation/flows`, `/automation/flows/{id}` | `automation.read` (view), `automation.manage` (edit, activate, test, delete) |
| Executions | `/automation/runs`, `/automation/runs/{id}` | `automation.read`; cancel: `automation.execute` or `automation.manage`; re-run: `automation.execute` |
| Schedules | `/automation/schedules` | `automation.read`; run now / pause: `automation.execute` / `automation.manage` (imports: `integration.imports.manage`) |
| Data sources | `/integration/datasources`, `/integration/datasources/{id}` | `integration.datasources.read`; edit: `…manage`; test / execute: `…execute` |
| Import sets | `/integration/imports`, `/integration/imports/{id}` | `integration.imports.manage` |
| Inbound webhook | `POST /api/v1/automation/webhooks/{flowId}` | flow secret (header or HMAC signature), 60 calls / min / IP |

Every application service method checks the permission itself (`Result.Forbidden()`), pages additionally carry
`[RequirePermission]`.

## Tables

| Table | Entity | Purpose |
|-------|--------|---------|
| `automation_flow` | `Flow` | Name, trigger (type, table + condition JSON, cron + time zone, poll endpoint), inputs JSON, steps JSON, definition version + note, next run, poll hash. |
| `automation_flow_run` | `FlowRun` | One execution: snapshot of the steps JSON and version, status, trigger summary, idempotency key (unique per flow), context JSON (trigger, inputs, vars, step outputs – redacted), wait state, parent run, cancel flag. |
| `automation_flow_run_step` | `FlowRunStep` | One step attempt: status, attempt, duration, redacted input / output snapshots (≤ 16 KB), error. |
| `int_data_source` | `DataSource` | Base URL, auth type and non-secret auth settings, default headers, timeout, retries, max response size, rate limit, HTTP allowed, enabled. |
| `int_data_source_endpoint` | `DataSourceEndpoint` | Key, method, path template, allowed parameters JSON, body template, response path. |
| `int_data_source_log` | `DataSourceExecutionLog` | Execution log (no parameter values, no credentials). |
| `int_network_allow_list` | `NetworkAllowListEntry` | Admin-approved internal CIDR ranges for the SSRF guard. |
| `int_import_set` / `int_import_run` / `int_import_row` | `ImportSet`, `ImportRun`, `ImportRow` | Import definition with transform map and schedule, runs with counters, staged rows with result. |

These are configuration / history tables (not `[RecordTable]`), so they raise no record events. Secrets (webhook
secrets, API keys, tokens, passwords, OAuth client secrets) live only in `ISecretStore`.

## Flow model

* **Triggers** (`FlowTriggerType`): `Manual` (UI, business rules, REST API, MCP, sub-flow), `RecordCreated`,
  `RecordUpdated`, `RecordCreatedOrUpdated` (table + `ConditionGroup`, changed/changed-to operators supported),
  `Schedule` (5-field cron + time zone), `Webhook` (secret per flow), `DataSourcePoll` (approved endpoint on a cron,
  optionally only when the response hash changed).
* **Steps** form a tree (`FlowStepDefinition`): `Type`, `Key` (data pill name), `Config` (string values that may
  contain pills; structured values such as field maps and conditions are JSON strings), `Branches` (lanes),
  `RetryCount` / `RetryDelaySeconds`, `ContinueOnError`, `Disabled`, `Notes`. Limits: 200 steps, nesting depth 8.
* **Action registry** (`IFlowAction`, extension point): register `services.AddScoped<IFlowAction, MyAction>()`; the
  designer palette, configuration drawer and data pill picker are generated from the `FlowActionDescriptor`.

| Type | Step | Notes |
|------|------|-------|
| `record.create` / `record.update` | Create / update record | Field values via `IRecordService` (permissions, business rules and audit apply). Outputs the record. |
| `record.lookup` | Look up records | Condition with pills, 1–100 records, outputs `count`, `total`, `first`, `records`. |
| `record.note` | Add work note | Work note or comment through `IActivityService`. |
| `flow.if` | If / else | Pill condition (AND/OR of rules), lanes `then` / `else`. |
| `flow.wait` | Wait | Duration or until a date (max. 90 days); the run sleeps in the database. |
| `approval.request` | Ask for approval | `IApprovalService`; waits, polls the decision, lanes `approved` / `rejected` (also timeout / cancel). |
| `email.send` | Send e-mail | `IEmailSender`; recipients, subject, body with pills (max. 50 recipients). |
| `datasource.call` | Call data source | `IDataSourceExecutor`; outputs `statusCode`, `json`, `body`, `durationMs`. |
| `flow.set` | Set variable | `vars.<name>`, values keep their JSON type. |
| `flow.log` | Log | Writes to the run history and application log. |
| `flow.subflow` | Run sub-flow | Optional wait for completion (depth ≤ 5), outputs the child's variables. |

### Data pills – `FlowTemplate`

`{{trigger.record.ShortDescription}}`, `{{steps.lookup.count}}`, `{{steps.lookup.records[0].Name}}`,
`{{inputs.name}}`, `{{vars.approver}}`, `{{run.id}}`, `{{flow.name}}`. An expression is a dotted path (identifiers
and indexes, `count` / `first` on arrays) plus whitelisted filters `upper`, `lower`, `trim`, `json`, `urlencode`,
`default:'x'`. There is **no code execution**: resolution is single-pass (values containing `{{…}}` are never
evaluated again), private segments (`_…`) are unreachable, the regex has a timeout and output is capped at 200 KB.
A template that is exactly one expression keeps the JSON type (numbers stay numbers). `FlowDataPillBuilder` offers
only pills of steps that are guaranteed to have run before the configured step (preceding siblings and ancestors).

## Engine

* `IFlowExecutionService` (`FlowExecutionService`) replaces the kernel placeholder; `IFlowRunStarter` snapshots the
  definition into a `FlowRun` and queues it. The **idempotency key** (`rec:<table>:<id>:<change>:<ticks>`,
  `cron:<ticks>`, `wh:<Idempotency-Key>`) is unique per flow, so redelivered events never start a flow twice.
* `FlowEngineWorker` executes runs with **bounded concurrency** (`Servora:Automation:MaxConcurrentRuns`, default 4),
  each in its own DI scope as the system actor `Flow: <name>` (audit fields show the flow). Runs interrupted by a
  restart are requeued; waiting runs are resumed when `ResumeAt` is due (`ResumePollSeconds`).
* `FlowRunner` replays completed steps from the context (following the recorded branch), executes the rest, persists
  progress after every step, retries retryable failures with exponential backoff (`RetryDelaySeconds × 2^n`, ≤ 300 s),
  enforces a per-step timeout (`StepTimeoutSeconds`) and a step budget (`MaxStepExecutionsPerRun`), and stops at the
  first waiting step. Cancellation sets `CancelRequested`, signals the executing run and cancels waiting sub-flows.
* `RecordTriggerHandler` (`IRecordEventHandler`) matches active record flows (30 s cache, invalidated on save /
  activation), evaluates the trigger condition with `IConditionEvaluator`, skips changes made by the same flow and
  builds `trigger.record` from exposed fields only (redacted).
* Redaction (`FlowRedactor`): properties whose names contain password, secret, token, api key, authorization,
  cookie, credential, … are masked in contexts and snapshots; long strings are truncated.

Options (`appsettings.json`, section `Servora:Automation`): `MaxConcurrentRuns`, `ResumePollSeconds`,
`SchedulerIntervalSeconds`, `ApprovalPollSeconds`, `SubflowPollSeconds`, `MaxStepExecutionsPerRun`,
`StepTimeoutSeconds`, `MaxSubflowDepth`, `RetentionDays` (history cleanup, default 90).

## Scheduler

`CronExpression` is an own 5-field parser (`minute hour day-of-month month day-of-week`) with lists, ranges, steps,
names (`JAN`, `MON`), `7` = Sunday, macros (`@hourly`, `@daily`, `@weekly`, `@monthly`, `@yearly`) and Vixie
day-of-month/day-of-week OR semantics. Occurrences are computed in an IANA or Windows time zone; local times in a DST
gap are skipped, repeated local times fire once. `SchedulerWorker` → `ScheduleProcessor` fires due schedule / poll
flows and scheduled imports and advances `NextRunAt` with a bulk update (does not bump the designer's concurrency
token). `/automation/schedules` lists all jobs with next-run preview, pause switch, run now and a cron playground.

## REST data sources and SSRF protection

* Only **approved endpoints** can be called: method, path template (`{name}` placeholders, URL-encoded, dot segments
  rejected, never leaves the base URL), declared parameters with type / pattern (regex with timeout) / max length /
  enum validation, JSON body template (`{{name}}` JSON-encoded) and an optional response JSON path.
* Auth: none, API key header, bearer, basic (refused over HTTP), OAuth 2.0 client credentials (token cached until
  expiry, token URL is SSRF-checked too). Credentials are write-only in the UI and stored in `ISecretStore`.
* `SsrfGuard` + `DataSourceHttp` handler:
  * HTTPS only unless the source allows HTTP; URLs with user info are rejected.
  * DNS is resolved and **every** address must be allowed; loopback, private, link-local, CGNAT, unique-local,
    documentation and IPv4-embedded IPv6 forms are blocked unless an admin allow-list range contains them. Cloud
    metadata addresses (`169.254.169.254`, `fd00:ec2::254`, …), multicast, unspecified and reserved ranges are
    **always** blocked.
  * The `ConnectCallback` re-resolves and connects only to an address it validated itself (no DNS rebinding window);
    proxies and cookies are disabled; certificate validation stays on.
  * Redirects are followed manually (max. 3), each target re-validated; HTTPS → HTTP downgrades are blocked and
    credentials are dropped on cross-origin redirects.
* Timeouts (1–120 s), retries for idempotent methods on 408 / 429 / 5xx with backoff, max response size (≤ 10 MB,
  truncated), per-source rate limit, execution log without parameter values.
* UI: list with KPIs and hourly call trend, global execution log, network allow-list; record page with connection,
  auth, default headers, limits, credential card, endpoint editor drawer, **Test endpoint** with response viewer
  (status, timing, size, whitelisted headers, body and mapped part) and per-source log.

## Import sets

Source: CSV upload (delimiter detection `,` `;` tab, quotes, ≤ 5 MB / 5 000 rows) or a JSON array returned by an
approved data source endpoint (`RecordsPath`, on demand or scheduled). Rows are **staged** (`int_import_row`), then
the transform map (source column / JSON path → target field, transform none / trim / upper / lower, default value)
is applied and records are written through `IRecordService` with the current user's permissions. A **coalesce
field** turns matching rows into updates (more than one match is reported as an error). Run history shows counters
per run and the staged rows with their result and target record.

## Seed data

`AutomationSeeder` (Order 300, idempotent by name): the demo data source *Open-Meteo Weather (demo)* with the
`current-weather` endpoint and two **inactive** example flows: *Daily weather digest (demo)* (weekday schedule, data
source call, variable, log) and *Welcome new users (demo)* (record created on the user table, if / else, e-mail).

## Tests

`tests/Servora.Tests.Unit/Automation` and `…/Integration`: cron parser (fields, names, macros, OR semantics, time
zones, DST gap / overlap, invalid input), template resolver (paths, filters, single pass, private segments),
engine sequencing (order, branches, wait / resume without repetition, retries, continue on error, cancellation,
idempotency, snapshots, redaction), acceptance scenarios (record trigger with condition + update + e-mail, scheduled
flow, approval wait / resume, data source step), flow service (versions, concurrency, activation rules, test runs,
webhook secret / HMAC, permissions), SSRF guard and address policy, HTTP invoker (mapping, auth, retries,
truncation, blocked base URLs and redirects), data source administration and import sets. A live check against a
public HTTPS API runs with `SERVORA_NETWORK_TESTS=1`.
