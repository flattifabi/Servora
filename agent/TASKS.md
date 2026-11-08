# Servora – Task Board (single source of truth)

Protocol: [`ORCHESTRATION.md`](ORCHESTRATION.md) · Decisions: [`DECISIONS.md`](DECISIONS.md) · Spec: [`../PROJECT_SPEC.md`](../PROJECT_SPEC.md)

Status legend: ⏳ Planned · 🔨 In Progress · 📨 Submitted · 🔍 In Review · 🔁 Rework · ✅ Done · ⛔ Blocked

## Product owner directives (binding, override the spec where they conflict)

- Open-source, self-hosted ServiceNow alternative in **Blazor** with backend; administration and configuration
  **only through the UI**. Feature parity goal: everything ServiceNow offers (ITSM, catalog, knowledge, CMDB, assets,
  automation, integrations, analytics, AI) – delivered incrementally.
- Databases: **SQL Server, SQLite, PostgreSQL**.
- UI must look **premium, enterprise-grade and outstanding** – not a standard scaffold.
- Clean architecture, clean English code documentation, **one class per file**.
- Automations, REST data sources, configuration, **Active Directory** integration for Windows users, own
  permission management (local and via AD) – must also work **without AD** using local users.
- Bootstrap administrator **`admin` / `admin`** (implemented with mandatory password change at first sign-in, ADR-006).
- Runs as a **Windows Service**; ship an **installer for GitHub releases** that installs or updates the service.
- Analytics; logging in UI and backend with a **debug switch**; REST interface; **MCP server**; **API key management** in the UI.
- No additional runtime downloads, **no licensed NuGet packages**; all documentation in English Markdown.
- **Database migrations are applied automatically** at startup (no manual step after install/update), guarded by an
  automatic pre-migration backup and automatic restore if a migration fails.
- **Built-in, configurable backup**: scheduled backups (e.g. weekly on a chosen day and time), retention, manual
  backups, download/upload, and **restore / roll back** to any backup from the UI (and command line for disaster
  recovery).

## Overview

| ID | Task | Owner (agent) | Depends on | Status |
|----|------|---------------|-----------|--------|
| T-01 | Kernel: solution, architecture, persistence, auth pipeline, design system, contracts | Orchestrator | – | ✅ Done |
| T-02 | Identity & Access: users, groups, roles, LDAP/AD, Windows SSO, profile | Identity Agent | T-01 | ✅ Done |
| T-03 | Setup wizard, system settings, branding, SMTP e-mail, secrets admin, database admin | Configuration Agent | T-01 | ✅ Done |
| T-04 | Incident & Problem Management + "My Work" workspace | ITSM Agent | T-01 | ✅ Done |
| T-05 | Change Management, Service Catalog, Requests, Approvals engine | Service Mgmt Agent | T-01 | ✅ Done |
| T-06 | Knowledge Management, CMDB (relationships, graph), Asset Management | Knowledge/CMDB Agent | T-01 | ✅ Done |
| T-07 | Platform engine: table registry, generic records & forms, conditions, custom tables/fields, business rules, SLAs, notifications, Table REST API, global record lookup | Platform Agent | T-01 | ✅ Done |
| T-08 | Automation & Integration: Flow Designer + engine, scheduler, REST data sources (SSRF-safe), import sets | Automation Agent | T-01 | ✅ Done |
| T-09 | Observability & Analytics: logging + debug switch, log viewer, audit viewer, health, dashboards, report builder | Observability Agent | T-01 | ✅ Done |
| T-10 | API keys, REST API foundation & docs, MCP server, AI providers / orchestrator / assistant | API & AI Agent | T-01 | ✅ Done |
| T-11 | Self-service portal | Portal Agent | T-04, T-05, T-06 (merged) | ✅ Done |
| T-12 | Windows service installer, GitHub release workflow, operations docs | DevOps Agent | T-01 | ✅ Done |
| T-13 | Integration: merge, provider migrations, cross-module wiring, end-to-end verification | Orchestrator | T-02 … T-12 | ✅ Done |
| T-14 | Security & architecture review (spec §14.2 gates) and fixes | Review Agent | T-13 | ⏸ Paused |
| D-1 | Design system redesign (light forest-green look from product owner references, brand-derived tokens, shell, sign-in) | UI Agent | T-13 | ✅ Done |
| P-1 | Self-service portal + catalog redesign (home, item configuration with live price summary, portal settings, catalog model + migration) | UI Agent | T-13 | ✅ Done |
| T-15 | Backup & restore (scheduled, retention, roll back), automatic migrations hardening | Backup Agent | T-01 | ✅ Done |
| G-1 | Clean start without sample data, setup option, Reset data with backup (getting-started guide still open) | Orchestrator | T-13 | ✅ Done |

Dependency graph:

```
T-01 ─┬─► T-02 ─┐
      ├─► T-03 ─┤
      ├─► T-04 ─┼─► T-11 ─┐
      ├─► T-05 ─┤         │
      ├─► T-06 ─┘         ├─► T-13 ─► T-14
      ├─► T-07 ───────────┤
      ├─► T-08 ───────────┤
      ├─► T-09 ───────────┤
      ├─► T-10 ───────────┤
      ├─► T-12 ───────────┤
      └─► T-15 ───────────┘
```

Cross-module calls go through kernel contracts with placeholders (ORCHESTRATION.md §5), so T-02…T-10 and T-12 run
in parallel without blocking each other.

---

## T-01 Kernel ✅

**Result:** Solution `Servora.slnx` (.NET 10): Domain / Application / Infrastructure / 3 persistence providers / Web
host / unit tests. Delivered: EF Core context with audit fields, optimistic concurrency, before-save hooks and async
record events; provider abstraction (SQLite, SQL Server, PostgreSQL) with encrypted `database.json`; Data Protection
keys (DPAPI on Windows); actor model (`ICurrentUser`, `ActorContext`) for users / API keys / agents / system;
RBAC with permission catalog, dynamic `perm:` policies and `[RequirePermission]`; cookie auth with security-stamp
revalidation, lockout, password policy, forced password change; bootstrap roles + `admin/admin`; hash-chained audit
log; number sequences; activity stream & attachments; directory lookups; setup guard; security headers; rate
limiting; Windows Service hosting; design system (`servora.css`), icon set, app shell (sidebar, top bar, command
palette Ctrl+K, toasts), 30+ UI components incl. SVG charts; login / logout / change-password pages; module stubs
for every task; placeholder implementations for cross-module contracts.

**Evidence:** `dotnet build Servora.slnx` → 0 errors, 0 warnings. `dotnet test` → 23 passed. Manual run: login as
admin/admin → forced password change (policy rejects weak / user-name passwords) → interactive dashboard.

---

## T-02 Identity & Access ✅

**Owned paths:** `*/Identity/**` (Domain additions, Application, Infrastructure, Configurations), `Components/Pages/Admin/Identity/**`,
`Components/Pages/Account/**` (may restyle/extend Login, ChangePassword), `Components/Pages/Profile/**`,
`IdentityModule.cs`, `IdentityEndpoints.cs`, `IdentityWebModule.cs`, `Permissions.Identity.cs`, `docs/modules/identity.md`.

**Scope**
1. Admin UI `/admin/users` (list, search, filters active/source/role, KPI cards), `/admin/users/{id}` and new user:
   profile fields, activation, roles, groups, reset password (sets MustChangePassword), unlock, force sign-out
   (rotate security stamp), source badge (Local / Directory / Windows).
2. `/admin/groups` + detail: members (add/remove), roles granted by group, type (assignment/security/approval), manager.
3. `/admin/roles` + detail: permission matrix grouped by `PermissionCatalog` groups with search, system-role
   protection (cannot delete, `admin` keeps `*`), privilege-escalation guard (a user can only grant permissions they hold).
4. LDAP / Active Directory: configuration page `/admin/directory` (host, port, LDAPS/StartTLS/None (warning),
   certificate validation, base DN, bind DN + password via `ISecretStore`, user/group search base & filter,
   attribute mapping, group→role mapping table, sync schedule, enable switch, "Test connection" and "Test user
   lookup"). `IExternalCredentialValidator` implementation using `System.DirectoryServices.Protocols` (safe filter
   escaping RFC 4515, timeouts, no anonymous bind fallback, never downgrade TLS silently). Just-in-time provisioning
   of users on first login; group membership → roles mapping (`IsFromDirectory` flags). Background directory sync
   (hosted service, interval configurable, bounded) and a "Sync now" button with result summary.
5. Windows integrated authentication (Negotiate/Kerberos/NTLM): `/auth/windows` endpoint challenging the Negotiate
   scheme (registered in `IdentityWebModule`), maps the Windows identity (SID / `DOMAIN\user`) to a user (JIT
   provisioning from AD when enabled), issues the normal cookie via `UserPicker`-independent `UserPrincipalFactory`.
   Toggle on `/admin/directory`; the login page button is shown only when enabled.
6. `/admin/auth` authentication policy: password policy values, lockout threshold/duration, session lifetime,
   allow local login toggle (break-glass: `admin` local login always allowed), stored as system settings.
7. `/profile`: own profile (name, phone, time zone, theme), my roles & groups, my sessions info, change password link.
8. Audit every identity/role/permission/directory change; invalidate `IPermissionService` caches on changes.
9. Seed demo data (idempotent): groups "Service Desk", "Network Operations", "Database Administration",
   "Change Advisory Board" (approval), a few demo users (fulfillers and employees) **with `MustChangePassword=true`
   and random passwords not shown anywhere**, memberships.

**Acceptance criteria:** CRUD for users/groups/roles works end-to-end; LDAP login works against AD when configured
and the app works fully without AD; Windows SSO endpoint works when enabled; escalation guard + audit present;
unit tests for LDAP filter escaping, group→role mapping and escalation guard.

**Result (validated by orchestrator, merged `af401eb`):** Users/groups/roles admin with permission matrix and escalation guard, LDAP/AD (LDAPS/StartTLS, group→role mapping, JIT provisioning, scheduled sync), Windows SSO via Negotiate, `/admin/auth` policy, `/profile`, idempotent demo seed.
**Evidence:** 130 files in owned paths; build 0/0; tests 144 passed (121 identity). Merged main: build 0 errors / 0 warnings, 707 tests passed, startup smoke on fresh SQLite healthy.
**Open follow-ups:** Not tested against a real AD domain; apply saved `User.Theme` in layout (T-13).

---

## T-03 Setup, Configuration & Secrets ✅

**Owned paths:** `*/Configuration/**`, `Components/Pages/Setup/**`, `Components/Pages/Admin/Configuration/**`,
`src/Servora.Web/Setup/**` (except `SetupGuardMiddleware`, `SetupState` which you may extend), `ConfigurationModule.cs`,
`ConfigurationEndpoints.cs`, `ConfigurationWebModule.cs`, `Permissions.Configuration.cs`, `docs/modules/configuration.md`.

**Scope**
1. **Setup wizard** `/setup` (AuthLayout, beautiful stepper): Welcome → Database (choose SQLite / SQL Server /
   PostgreSQL with provider cards, connection builder fields + raw connection string, "Test connection") →
   Organisation (company name, product name, accent color, time zone) → Summary → Install (saves via
   `DatabaseSettingsStore`, runs `DatabaseInitializer`, shows progress, admin credentials hint `admin/admin` +
   forced change) → "Go to sign-in". Remote (non-loopback) access to the wizard requires a setup token written to
   `<data dir>/setup-token.txt` on startup while unconfigured. Use `SetupState.IsInProgress` while finishing.
2. **System settings** `/admin/settings`: typed configuration service `ISystemSettingsService` (get/set typed values,
   defaults, categories, change history via audit) and UI with categories (General, Branding, Localization,
   E-mail, Security headers info, Features). Settings take effect without restart where possible; show a
   "restart required" badge otherwise.
3. **Branding**: product name, company name, logo (upload → stored safely), accent color; expose
   `IBrandingService` for layout use (request to orchestrator to wire into NavMenu/AuthLayout).
4. **E-mail**: SMTP settings (host, port, TLS mode, user, password in `ISecretStore`, from address), "Send test
   e-mail"; real `IEmailSender` (System.Net.Mail, async, timeout, no credential logging) + outbound mail queue
   with retry and an "Outbox" view.
5. **Secrets** `/admin/secrets`: list (name, description, last rotated – never values), create, rotate (write-only
   input), delete with confirmation; audit.
6. **Database** info card (provider, redacted connection string, schema status, size where available) and
   "Test connection" in settings.
7. **Feature flags** stored as settings (`features.*`) with a toggle UI.

**Acceptance criteria:** fresh data dir → wizard → working instance on SQLite (and connection test for SQL Server /
PostgreSQL); settings persist and audit; test e-mail works against an SMTP server; secrets never displayed after save;
unit tests for settings typing/defaults and SMTP option validation.

**Result (validated by orchestrator, merged `e8fda35`):** Setup wizard (loopback or setup token, no query-string token), typed system settings with history and restart badge, branding + logo (magic-byte checked), SMTP sender + outbox with retry, secrets admin (write-only), database card trimmed to the T-15 boundary, feature flags.
**Evidence:** All files in owned paths; build 0/0; tests 121 passed; wizard verified end to end on SQLite. Merged main: build 0 errors / 0 warnings, 707 tests passed, startup smoke on fresh SQLite healthy.
**Open follow-ups:** Implicit TLS (465) unsupported by System.Net.Mail; branding wiring into layout (T-13).

---

## T-04 Incident & Problem Management + My Work ✅

**Owned paths:** `*/Itsm/**` (excluding kernel files `TaskRecord.cs`, `Priority*.cs`, `Impact.cs`, `Urgency.cs`,
`TaskRecordConfigurationExtensions.cs` – read-only), `Components/Pages/Itsm/**`, `Components/Itsm/**`,
`Components/Pages/Home.razor` (replace placeholder), `ItsmModule.cs`, `ItsmEndpoints.cs`, `ItsmWebModule.cs`,
`Permissions.Incidents.cs`, `Permissions.Problems.cs`, `docs/modules/itsm.md`.

**Scope**
1. `Incident : TaskRecord` (`incident`): state machine New → In Progress → On Hold (reason) → Resolved (resolution
   code + notes, required) → Closed / Cancelled; caller, category/subcategory, channel (portal, phone, e-mail,
   monitoring, chat), business service (CI), major incident flag, parent incident, problem link, reopen count,
   resolved at/by. Priority from `PriorityMatrix` via a saving hook. Number prefix `INC`.
2. `Problem : TaskRecord` (`problem`): New → Assess → Root Cause Analysis → Fix in Progress → Resolved → Closed;
   known error flag, workaround, root cause, fix notes, related incidents. Prefix `PRB`.
3. Services with permission checks; state transition validation; auto-assignment helper (assign to me);
   resolve/close/reopen actions; link incidents to problem; create problem from incident.
4. List pages `/incidents`, `/problems`: KPI row (open, P1, unassigned, breached SLA via `ISlaService` if any),
   saved quick filters (My open, Unassigned, P1/P2, All active, Resolved), DataTable with number, priority badge,
   state pill, short description, caller/assignee avatars, group, updated time; bulk assign.
5. Record pages `/incidents/new`, `/incidents/{id}` (and problems): enterprise form layout, `StateFlow`, header
   actions (Resolve, Close, Reopen, Assign to me, Create problem), side card (SLA timers, caller info, CI),
   tabs: Activity (`ActivityStream`), Related (child incidents / problem / incidents of problem), Attachments.
6. **My Work** (`/`): personalised workspace replacing the placeholder: greeting, KPI cards (assigned to me, my
   groups' unassigned, P1 open, my approvals placeholder count via `IApprovalService` where possible), my work
   list across incidents/problems, recent activity, 14-day trend chart of opened vs resolved incidents.
7. Seed: incident categories and ~25 realistic demo incidents + 4 problems (idempotent; only if table empty).

**Acceptance criteria:** full lifecycle works with validation; lists page/sort/search server-side; priority
calculated; activity/attachments work; unit tests for state machine and priority hook.

**Result (validated, merged `4ee2873`):** `Incident`, `Problem`, `IncidentCategory`; domain state machines; `ItsmSavingHook`
(priority matrix, transition validation, Active/resolved/closed timestamps – applies to every writer incl. Table API and
flows); `IIncidentService`, `IProblemService`, `IMyWorkService` with permission checks, optimistic concurrency and
field-change journal; list + record pages; My Work dashboard; idempotent seed (7 categories, 25 incidents, 4 problems).
**Evidence:** 62 files in owned paths; build 0/0; tests 74 passed on branch (51 new), 213 on main after merge; agent
smoke-tested all flows in the running app.
**Kernel findings fixed by orchestrator:** combo-menu z-index under click-catcher (pickers unclickable), pickers
clipped in modals/drawers, scoped CSS bundle not linked, h1 focus outline, tabs scrollbar. Workaround component removed.
Contracts extended (default interface methods): `IApprovalService.CountPendingForUserAsync`,
`ISlaService.GetBreachedRecordIdsAsync`.
**Follow-ups (T-13):** wire My Work approvals count once T-05 implements it; use bulk SLA lookup for KPI; attachment
file cleanup on record delete.

---

## T-05 Change, Catalog, Requests & Approvals ✅

**Owned paths:** `*/Changes/**`, `*/Catalog/**`, `*/Approvals/**` (Application `Approvals` contract files are
read-only, implement them), `Components/Pages/Changes/**`, `Components/Pages/Catalog/**`,
`Components/Pages/Requests/**`, `Components/Pages/Approvals/**`, `Components/ServiceManagement/**`,
`ServiceManagementModule.cs`, `ServiceManagementEndpoints.cs`, `ServiceManagementWebModule.cs`,
`Permissions.Changes.cs`, `Permissions.Requests.cs`, `Permissions.Approvals.cs`, `docs/modules/service-management.md`.

**Scope**
1. **Approvals engine** implementing `IApprovalService`: `Approval` entity (`sys_approval`, approver, state
   requested/approved/rejected/cancelled, comments, decided at), group expansion, policy any/all, record events so
   other modules can react; `/approvals` "My approvals" page (cards with record summary, approve / reject with comment).
2. **Change requests** `ChangeRequest : TaskRecord` (`change_request`, prefix `CHG`): types Normal / Standard /
   Emergency; states New → Assess → Authorize → Scheduled → Implement → Review → Closed / Cancelled; risk & impact
   assessment (simple questionnaire → risk level), implementation / backout / test plans, planned start/end,
   CAB approval via `IApprovalService` (approval group "Change Advisory Board"), conflict detection (overlapping
   changes on the same CI), change tasks (`change_task`). List + record pages, change calendar view (month grid).
3. **Service catalog**: categories, catalog items (name, short/long description, icon, price/delivery time,
   fulfilment group, approval required + approver group, active), variables (variable definitions: text,
   multiline, number, choice with options, boolean, date, user reference; mandatory, order, help text) – builder
   UI `/catalog-admin` and `/catalog-admin/{id}`.
4. **Requests**: `Request` (`sc_request`, `REQ`), `RequestItem` (`sc_req_item`, `RITM`, variable answers JSON),
   `CatalogTask` (`sc_task`, `SCTASK`); ordering service used by the portal (T-11) –
   `ICatalogOrderService.OrderAsync(itemId, variables, requestedFor)` with validation; on order: approval (if
   required) → task creation for fulfilment group → closing RITM/REQ when tasks complete. Fulfiller pages
   `/requests`, `/requests/{id}` with RITMs and tasks.
5. Seed: CAB group usage, 8 catalog items (New laptop, Software install, Access request, VPN, Mobile phone,
   Password reset, New employee onboarding, Shared mailbox) with variables; a few demo changes.

**Acceptance criteria:** change lifecycle with CAB approval works; catalog builder creates items with variables;
order → approval → tasks → closure works; approvals page works; unit tests for approval policy logic, change
state machine and variable validation.

**Result (validated by orchestrator, merged `6391ea7`):** Approvals engine + inbox, change lifecycle with CAB, risk questionnaire, conflicts, calendar, `ChangeSavingHook` for all writers, catalog builder (7 variable types), order → approval → tasks → closure, `/requests/new` order-on-behalf page.
**Evidence:** All files in owned paths; build 0/0; tests 76 passed. Merged main: build 0 errors / 0 warnings, 707 tests passed, startup smoke on fresh SQLite healthy.
**Open follow-ups:** Hook trusts the `Approval` field from Table API writers (T-14 review item); no dedicated REST endpoints yet.

---

## T-06 Knowledge, CMDB & Assets ✅

**Owned paths:** `*/Knowledge/**`, `*/Cmdb/**` (kernel `ConfigurationItem.cs`, `OperationalStatus.cs` may be
extended additively), `*/Assets/**`, `Components/Pages/Knowledge/**`, `Components/Pages/Cmdb/**`,
`Components/Pages/Assets/**`, `Components/KnowledgeCmdb/**`, `KnowledgeCmdbModule.cs`, `KnowledgeCmdbEndpoints.cs`,
`KnowledgeCmdbWebModule.cs`, `Permissions.Knowledge.cs`, `Permissions.Cmdb.cs`, `Permissions.Assets.cs`,
`docs/modules/knowledge-cmdb-assets.md`.

**Scope**
1. **Knowledge**: knowledge bases, categories, `KnowledgeArticle` (`kb_knowledge`, prefix `KB`): title, summary,
   body (Markdown rendered safely to HTML – implement a small, safe Markdown subset renderer with HTML encoding;
   no external packages), workflow Draft → Review → Published → Retired, versions, valid to, view count, helpful
   votes, attachments, related CIs. Pages: `/knowledge` (search-first hero, categories, popular/new),
   `/knowledge/{id}` (reader view with TOC), `/knowledge/new|edit/{id}` (editor with live preview).
   `IKnowledgeSearchService` (title/summary/body term search, ranked) for portal & AI.
2. **CMDB**: CI classes (`cmdb_class`: key, label, icon, parent class, class-specific attribute definitions stored as
   JSON schema → rendered as custom fields), `CiRelationship` (`cmdb_rel_ci`: parent, child, type "Depends on",
   "Runs on", "Hosted on", "Connects to", "Contains"), pages `/cmdb` (class filter chips, KPIs by status),
   `/cmdb/{id}` (attributes, relationships editor, **interactive SVG dependency graph** (radial / layered layout,
   upstream & downstream, click to navigate), related incidents/changes via `IRecordService` or direct queries
   guarded by permissions), `/cmdb/classes` admin.
3. **Assets** `Asset` (`alm_asset`, prefix `AST`): model, category (hardware/software/license), serial, asset tag,
   state (in stock, in use, in repair, retired, disposed), assigned to, location, cost, purchase date, warranty
   end, linked CI; software licenses (entitlements count vs. installs). Pages `/assets`, `/assets/{id}` with KPIs
   (value, expiring warranties).
4. Seed: CI classes (Business Service, Application, Server, Database, Network Device, Virtual Machine, Storage),
   ~30 CIs with relationships forming a meaningful service map, 8 KB articles, ~20 assets.

**Acceptance criteria:** KB workflow and search work, Markdown rendering is XSS-safe (tests), CMDB graph renders and
navigates, asset lifecycle works; unit tests for Markdown renderer safety, graph traversal and KB workflow.

**Result (validated by orchestrator, merged `dcd686a`):** Knowledge workflow with versions, ranked search, in-house XSS-safe Markdown, CMDB classes/attributes/relationships with bounded dependency graph (SVG) and map, asset lifecycle with KPIs, seed data, read-only REST endpoints.
**Evidence:** 155 files in owned paths; build 0/0; tests 176 passed (153 module). Merged main: build 0 errors / 0 warnings, 707 tests passed, startup smoke on fresh SQLite healthy.
**Open follow-ups:** Remove `ModuleStyles` workaround (T-13); CI related records need T-07 record service.

---

## T-07 Platform Engine ✅

**Owned paths:** `*/Platform/**` (replace kernel `ReflectionTableRegistry` usage by registering your own registry;
you may subclass it), `*/Conditions/**` implementations (contract files read-only), `*/Tables/**` implementations,
`*/Sla/**`, `*/Notifications/**` (except `IEmailSender`/`EmailMessage` contracts), `*/BusinessRules/**`,
`Components/Pages/Platform/**`, `Components/Platform/**`, `PlatformModule.cs`, `PlatformEndpoints.cs`,
`PlatformWebModule.cs`, `Permissions.Platform.cs`, `docs/modules/platform.md`.

**Scope**
1. `ConditionEvaluator` (`IConditionEvaluator`): all operators, type coercion (enums by value/name, Guid, dates,
   numbers, booleans), `IsMe`, change-aware operators; LINQ expression builder for EF (provider-neutral).
2. Table registry with **custom tables & custom fields** (`sys_db_object`-like `CustomTable`, `CustomField` with
   types, choices, reference table, mandatory, default, help) – custom fields on typed tables stored in
   `CustomFieldsJson`; custom table records in `CustomRecord` (`IDynamicTableRecord`, JSON data). Cache + invalidate.
3. `RecordService` (`IRecordService`): generic CRUD for typed + custom tables using EF metadata/reflection, field
   validation, `[NotExposed]` respected, table permissions (`table.<name>.read|create|write|delete` **or** the owning
   module permission mapped per table), display values for references, bounded paging.
4. Studio UI: `/admin/tables` (list typed + custom tables), `/admin/tables/{name}` (fields designer: add/edit custom
   fields, form layout ordering/sections, list default columns), generic list `/table/{name}` and generic form
   `/table/{name}/{id}` (rendering any table incl. custom fields, using kernel UI components), plus a reusable
   `CustomFieldsSection` component other modules can embed in typed forms.
5. **Business rules** (`sys_script`-like, declarative only – no code): table, when (before insert/update/delete,
   after async), condition (`ConditionBuilder` with change operators), actions: set field values, abort with
   message, add work note, start flow (`IFlowExecutionService`), send notification. Implemented via
   `IRecordSavingHook` (before) and `IRecordEventHandler` (after). UI `/admin/business-rules`.
6. **SLA engine**: SLA definitions (table, start / pause / stop conditions, target duration, schedule: 24×7 or
   business hours with time zone and weekdays), task SLA records, background evaluator (hosted service, bounded),
   breach → activity entry + notification; `ISlaService` implementation; UI `/admin/sla`.
7. **Notifications**: templates (subject/body with `${field}` placeholders, HTML-encoded), triggers (table, event,
   condition), recipients (field references like AssignedTo, group members, fixed addresses), delivery via
   `IEmailSender`; in-app notifications bell API (`INotificationInbox`) – request orchestrator to wire the TopBar bell.
   UI `/admin/notifications`.
8. **Table REST API** in `PlatformEndpoints`: `GET/POST /api/v1/table/{table}`, `GET/PATCH/DELETE /api/v1/table/{table}/{id}`
   with `sysparm_query`-like condition JSON, fields, paging limits; authorization via `api.access` + table
   permissions (API keys come from T-10; cookie auth also works). ProblemDetails errors.
9. `/go/{number}` page resolving a record number prefix (INC, PRB, CHG, REQ, RITM, SCTASK, KB, AST, …) to its route.

**Acceptance criteria:** custom table end-to-end (create table + fields in UI → records via generic list/form and
Table API); business rule set-field and abort work; SLA timer appears for a task and breaches; notification e-mail
rendered; comprehensive unit tests for the condition evaluator (in-memory and EF on SQLite).

**Result (validated by orchestrator, merged `eb294cf`):** Condition engine (in memory + EF), table registry with custom tables/fields, record service, table designer, generic list `/table/{name}` and form, business rules (set field / abort), SLA engine with business-hours schedules and breach monitor, notifications + templates + in-app inbox and bell, Table REST API (`/api/v1/table`), `/go/{number}`.
**Evidence:** All files in owned paths; build 0/0; tests 166 passed; custom table → records → rule verified in the app. Merged main: build 0 errors / 0 warnings, 960 tests passed, smoke on fresh SQLite healthy, API/MCP 401 without key, admin sign-in → forced password change.
**Open follow-ups:** Inbox POST endpoints without antiforgery token (low impact, T-14); no CSV export in lists.

---

## T-08 Automation & Integration ✅

**Owned paths:** `*/Automation/**` (contract files read-only, implement them), `*/Integration/**` (contracts
read-only), `Components/Pages/Automation/**`, `Components/Pages/Integration/**`, `Components/Automation/**`,
`AutomationModule.cs`, `AutomationEndpoints.cs`, `AutomationWebModule.cs`, `Permissions.Automation.cs`,
`Permissions.Integration.cs`, `docs/modules/automation-integration.md`.

**Scope**
1. **Flow model**: `Flow` (name, description, active, trigger, steps JSON, version), triggers: record created /
   updated (table + condition), scheduled (cron), manual, inbound webhook (authenticated, secret per flow),
   data source poll. Steps (extensible action registry `IFlowAction`): Create record, Update record, Look up records,
   Condition (if/else branches), Wait (duration), Ask for approval (`IApprovalService`, wait for decision), Send e-mail
   (`IEmailSender`), Call data source (`IDataSourceExecutor`), Add work note, Log, Set variable, Run sub-flow.
   Data pills / variables (`{{trigger.record.ShortDescription}}`, `{{steps.lookup.count}}`) with a safe template
   resolver (no code execution).
2. **Engine**: `IFlowExecutionService` implementation, `FlowRun` + `FlowRunStep` history (status, duration, input/
   output snapshot redacted, error), bounded concurrency (configurable), retries with backoff per step, cancellation,
   idempotency key per trigger event, waiting runs resumed by a background service, record-event trigger via
   `IRecordEventHandler`, actor = flow's run-as identity (system with audit).
3. **Flow Designer UI** `/automation/flows`, `/automation/flows/{id}`: premium vertical canvas with trigger card,
   step cards, connectors, branch lanes for conditions, add-step palette, step configuration drawer with data pill
   picker, test run button with live step results; activate/deactivate; version note.
4. **Executions** `/automation/runs` (filters, status pills, duration) + run detail timeline.
5. **Scheduler**: own cron parser (5-field, with tests), scheduled jobs list `/automation/schedules`, next-run preview.
6. **REST data sources** `/integration/datasources`: base URL, auth (none / API key header / bearer / basic over TLS
   / OAuth2 client credentials) with credentials in `ISecretStore`, default headers, endpoints (key, method, path
   template, allowed parameters with validation, body template, response JSON path mapping), timeout, retries,
   max response size, rate limit, enabled. **SSRF protection**: resolve DNS, block loopback/private/link-local/
   metadata ranges unless an admin allow-list entry exists, re-check on redirects (max 3), HTTPS-only by default,
   certificate validation always on. `IDataSourceExecutor` implementation, "Test endpoint" UI with response viewer,
   execution log.
7. **Import sets** `/integration/imports`: import from a data source endpoint (JSON array) or uploaded CSV into a
   staging set, transform map (source path → target table field via `IRecordService`, coalesce key for
   insert/update), run now / schedule, run history with row results.

**Acceptance criteria:** a record-triggered flow with condition + update + e-mail step runs end-to-end; scheduled
flow fires; approval wait resumes; data source executes against a public HTTPS API and blocks `http://127.0.0.1`
and `169.254.169.254`; import maps rows into a table; unit tests for cron parser, SSRF guard, template resolver and
engine step sequencing.

**Result (validated by orchestrator, merged `d77e5c0`):** Flow engine with record/schedule/webhook triggers, approval waits and sub-flows, cron scheduler, visual Flow Designer, SSRF-safe REST data sources (DNS + connect-time checks, metadata blocked, redirect re-validation), endpoint tester, import sets with transform maps (CSV + data source JSON), webhook with secret/HMAC.
**Evidence:** 230 files in owned paths; build 0/0; tests 399 passed (182 new). Merged main: build 0 errors / 0 warnings, 1146 tests passed, smoke on fresh SQLite healthy.
**Open follow-ups:** Set-variable input snapshot not masked; no pill picker in ConditionBuilder.

---

## T-09 Observability & Analytics ✅

**Owned paths:** `*/Observability/**` (kernel `AuditService`, `AuditEvent` may be extended additively),
`*/Analytics/**`, `Components/Pages/Observability/**`, `Components/Pages/Analytics/**`, `Components/Analytics/**`,
`ObservabilityModule.cs`, `ObservabilityEndpoints.cs`, `ObservabilityWebModule.cs`, `Permissions.Observability.cs`,
`Permissions.Analytics.cs`, `docs/modules/observability-analytics.md`.

**Scope**
1. **Logging**: rolling JSON-lines file logger provider (daily files, size cap, retention days) in the data
   directory + in-memory ring buffer (last N entries) – both via `ObservabilityWebModule` on `builder.Logging`;
   correlation id scope middleware; secret redaction filter (patterns for passwords, tokens, connection strings).
2. **Debug switch**: runtime minimum level (Information ↔ Debug ↔ Trace) global and per category prefix, persisted
   as setting, applied without restart; optional auto-revert after N minutes. UI `/admin/logs`: live tail
   (auto-refresh), level/category/text filters, detail drawer with exception, download filtered logs.
3. **Audit viewer** `/admin/audit`: filters (category, action, actor, outcome, date range), detail drawer, CSV export
   (permission `admin.audit.read`), **hash-chain verification** button showing integrity status; retention setting.
4. **Health** `/admin/health` and `/health` (JSON, anonymous minimal / authenticated detailed): database
   connectivity & latency, disk space of data dir, record event queue backlog, background services heartbeat,
   memory/CPU/uptime/version; ASP.NET Core health checks registration.
5. **Metrics**: request count/latency/error middleware (in-memory aggregation per minute, last 24 h), exposed to UI.
6. **Analytics dashboards** `/analytics`: executive dashboard with KPI cards and charts (incidents opened vs
   resolved, backlog by priority, MTTR trend, SLA compliance, changes by state, requests by item, top categories,
   automation success rate) – computed by `IAnalyticsService` from typed tables (handle missing modules gracefully).
7. **Report builder** `/analytics/reports`, `/analytics/reports/{id}`: choose table (`ITableRegistry`), filter
   (`ConditionBuilder`), group by field, aggregate (count/sum/avg), chart type (bar, line, donut, table), save &
   share (private / role), pin to a personal dashboard; uses `IRecordService` when available, falls back to typed
   queries for core tables.

**Acceptance criteria:** debug switch changes log level live; logs visible in UI and on disk with redaction tests;
audit verification detects tampering (test); health page shows real data; dashboards render with seed data; report
builder saves and renders a grouped chart.

**Result (validated by orchestrator, merged `0fe2a8e`):** Logging with live debug switch (persisted, auto-revert), log viewer + daily JSONL files with redaction, audit viewer with hash-chain verification surviving retention, health page with real metrics, executive dashboard, report builder with sharing and pins.
**Evidence:** 154 files in owned paths; build 0/0; tests 92 passed. Merged main: build 0 errors / 0 warnings, 707 tests passed, startup smoke on fresh SQLite healthy.
**Open follow-ups:** Metrics in memory only (24 h); report filters need T-07 condition evaluator.

---

## T-10 API, MCP & AI ✅

**Owned paths:** `*/Api/**`, `*/Mcp/**`, `*/Ai/**`, `Components/Pages/Api/**`, `Components/Pages/Mcp/**`,
`Components/Pages/Ai/**`, `Components/Ai/**`, `ApiMcpAiModule.cs`, `ApiMcpAiEndpoints.cs`, `ApiMcpAiWebModule.cs`,
`Permissions.Api.cs`, `Permissions.Mcp.cs`, `Permissions.Ai.cs`, `docs/modules/api-mcp-ai.md`.

**Scope**
1. **API keys** `/admin/api-keys`: create (name, description, owner user or service identity, scopes from the
   permission catalog limited to the creator's own permissions, expiry, allowed IP ranges, rate limit), secret shown
   **once** (`srv_<id>_<secret>` format), SHA-256 hash storage with constant-time comparison, rotate, revoke,
   last-used tracking, usage chart. Authentication handler scheme `ApiKey` (header `X-Api-Key` or
   `Authorization: Bearer srv_…`) producing a principal with `ServoraClaimTypes.ActorType=apikey`, `ApiKeyId`,
   `Permission` scope claims and owner user id (registered in `ApiMcpAiWebModule`, policy scheme forwarding for
   `/api` and `/mcp`). Per-key rate limiting. Audit lifecycle + failed auth.
2. **REST API foundation**: `/api/v1` conventions (ProblemDetails, correlation id header, paging limits),
   OpenAPI document via `Microsoft.AspNetCore.OpenApi` at `/openapi/v1.json`, and a custom, beautiful **API Explorer**
   page `/admin/api-docs` (lists operations from the OpenAPI document, try-it with the user's cookie, curl snippets).
   Core endpoints: `GET /api/v1/me`, `GET /api/v1/health`. (Modules add their own endpoints.)
3. **MCP server** at `/mcp` implementing the Model Context Protocol over **Streamable HTTP** (JSON-RPC 2.0):
   `initialize`, `ping`, `tools/list`, `tools/call`, `resources/list`, `resources/read`; protocol version
   negotiation (document supported version in docs). Authentication via API keys (`mcp.access`); every tool call
   authorized by scope `mcp.tool.<name>` **and** the underlying permission; inputs validated against JSON schema;
   bounded outputs; audit of each call. Tools: `search_records` / `get_record` (via `IRecordService`),
   `create_incident`, `list_data_sources` / `execute_data_source` (`IDataSourceExecutor`), `list_flows` /
   `start_flow` / `get_flow_run` (`IFlowExecutionService`), `search_knowledge` (via record service on
   `kb_knowledge`), `platform_health`. Admin page `/admin/mcp`: enable switch, tool catalog with per-tool enable,
   client connection snippet (Claude Desktop / generic JSON), recent invocations.
4. **AI providers** `/admin/ai`: provider configs (OpenAI-compatible endpoint incl. local Ollama/LM Studio,
   Anthropic Messages API, Azure OpenAI) with API keys in `ISecretStore`, model selection, test button; all calls via
   `HttpClient` (no SDK packages); timeouts and token limits.
5. **AI orchestrator & sub-agents**: `IAiOrchestrator` that receives a request, plans, delegates to registered
   sub-agents (Triage agent: classify/prioritise incident text; Knowledge agent: answer from KB search results;
   Record agent: look up records; Automation agent: suggest/start flows) using tool calling where the provider
   supports it; agents run with their own scoped `ActorInfo` (no permission escalation; consequential actions
   require explicit user confirmation in the UI); execution trace persisted (`AiConversation`, `AiMessage`, tool
   calls) and visible.
6. **AI Assistant** `/assistant`: premium chat UI (streaming-style rendering, suggestions, tool-call cards showing
   which agent did what, confirmation buttons for actions, conversation history sidebar). Works with a clear
   "configure a provider" empty state when none is configured.

**Acceptance criteria:** API key auth works for `/api/v1/me` and is revocable; secrets never retrievable; MCP
`initialize` → `tools/list` → `tools/call` works with an API key and is denied without scope; AI assistant answers
via a configured OpenAI-compatible endpoint; unit tests for key hashing/verification, MCP JSON-RPC dispatch and
input validation, orchestrator delegation with a fake provider.

**Result (validated by orchestrator, merged `7c14f98`):** API keys (SHA-256 hash, shown once, scopes ≤ creator, IP ranges, rotate/revoke), REST foundation with OpenAPI + API Explorer, in-house MCP server (`/mcp`, scoped tools, limits, audit), AI providers (OpenAI-compatible, key in secret store), orchestrator with 4 sub-agents, assistant with confirmation cards.
**Evidence:** 207 files in owned paths; build 0/0; tests 133 passed (110 new); MCP/API verified against the running app. Merged main: build 0 errors / 0 warnings, 960 tests passed, smoke on fresh SQLite healthy, API/MCP 401 without key, admin sign-in → forced password change.
**Open follow-ups:** No token streaming, no MCP SSE/OAuth; default role grants for `api.access`/`mcp.access`/`ai.assistant.use` to decide (T-13); host auth/rate-limit wiring lives in `ApiMcpAiWebModule` (review in T-14).

---

## T-11 Self-Service Portal ✅ (wave 2 – T-04/T-05/T-06 merged)

**Owned paths:** `*/Portal/**`, `Components/Pages/Portal/**`, `Components/Portal/**`, `PortalModule.cs`,
`PortalEndpoints.cs`, `PortalWebModule.cs`, `Permissions.Portal.cs`, `docs/modules/portal.md`.

**Scope:** consumer-grade portal (own `PortalLayout` with hero search): home (search KB + catalog, popular items,
my open requests/incidents, announcements), catalog browsing & item order form rendering variables, "Report an
issue" (creates incident as caller), my requests & incidents (status timeline, comments as caller via
`ActivityStream` without work notes), knowledge reader, approvals for approvers. Users with only `portal.access`
can use it fully; data access restricted to own records.

**Result (validated by orchestrator, merged `c8dcc55`):** `/portal` with own `PortalLayout`, hero search (knowledge, catalog, own items), home with announcements and KPIs, catalog ordering via `ICatalogOrderService`, report an issue (caller = user, KB suggestions, attachments), my items with status timelines, accept/reopen, cancel, comment-only conversation, knowledge reader with votes, approvals, announcements admin (`portal.manage`).
**Evidence:** 105 files in owned paths; build 0/0; tests 741 passed (34 portal tests proving a `portal.access`-only user cannot see or act on other users' records). Merged main: build 0/0, **1298 tests passed**, smoke healthy.
**Open follow-ups:** knowledge search/vote logic duplicated (portal users lack `knowledge.read`); portal-only users landing on `/` see access denied until the redirect is added (T-13).

---

## T-12 Installer, Windows Service & Release ✅

**Owned paths:** `src/Servora.Installer/**` (new console project – request orchestrator to add it to the solution
if you cannot), `.github/workflows/**`, `build/**`, `docs/operations/**`.

**Scope**
1. `Servora.Installer` (.NET console, self-contained single-file `ServoraSetup.exe`, requires elevation – app
   manifest `requireAdministrator`): detects existing service `Servora` → **update** (stop service, back up
   current binaries to `%ProgramData%\Servora\backups\<timestamp>`, copy new files, keep data dir and instance
   `appsettings.json`, start service, health check `/health`, automatic rollback on failure) or **install** (copy to
   `%ProgramFiles%\Servora`, create service via `sc.exe`/ServiceController with automatic (delayed) start,
   recovery actions restart, optional firewall rule via `netsh`, port selection, open browser to setup wizard).
   Commands: `install`, `update`, `uninstall [--keep-data]`, `status`, interactive menu when run without args,
   `--silent`. Payload: zipped published app embedded as resource or side-by-side `app/` folder.
2. GitHub Actions: `ci.yml` (build + test on push/PR, windows-latest), `release.yml` (on tag `v*`: publish
   `win-x64` self-contained, build installer, create `Servora-<version>-win-x64.zip` + `ServoraSetup-<version>.exe`
   + SHA256 checksums, create GitHub release with notes from CHANGELOG).
3. `build/publish.ps1` local script producing the same artifacts.
4. Docs: `docs/operations/installation.md`, `upgrade.md` (incl. rollback), `windows-service.md`, `backup-restore.md`
   (per provider), `configuration.md` (data dir, `appsettings.json` overrides, HTTPS certificate configuration with
   Kestrel, reverse proxy), `troubleshooting.md`.

**Acceptance criteria:** `build/publish.ps1` produces artifacts locally; installer `status` works without admin,
install/update logic unit-tested via abstractions (file system + service controller interfaces); workflows valid YAML.

**Result (validated by orchestrator, merged `ae96d51`):** `ServoraSetup.exe` with install / update (backup, health
check, automatic rollback, backup pruning, downgrade guard) / uninstall / status, interactive menu and silent mode;
embedded payload zip (single download); virtual account `NT SERVICE\Servora`, quoted binary path, delayed auto start,
recovery actions, hardened ACLs, firewall rule (domain/private only). `ci.yml`, `release.yml`, `build/publish.ps1`,
six operations docs. Deviation accepted: manifest `asInvoker` + UAC self-relaunch (so `status` runs unelevated).
**Evidence:** 98 files all in owned paths; build 0 errors / 0 warnings; tests 162 passed (139 new installer tests);
publish produced `Servora-0.1.0-win-x64.zip` and `ServoraSetup-0.1.0-win-x64.exe`.
**Open follow-ups:** end-to-end install on an elevated VM; code signing; pin GitHub Action SHAs; forwarded-headers
config (T-13); keep `/health` anonymous while unconfigured (T-03/T-09 – already anonymous in kernel).

---

## T-13 Integration ✅ (Orchestrator)

**Split (2026-11-08):** T-13a *Migrations* (`src/Servora.Persistence.*`, `DatabaseInitializer.cs`, migration tests, `docs/development/migrations.md`) and T-13b *Cross-module integration* (Web/layout/navigation/auth/seeders, no EF model changes) run in parallel worktrees; the orchestrator merges both and verifies migrations against the merged model.

Merge all branches, resolve conflicts, process "Requests to orchestrator" (nav entries, shared files, packages),
wire cross-module UI (branding into layout, notification bell, custom fields section in typed forms), generate
`InitialCreate` migrations for SQLite, SQL Server and PostgreSQL, run full build/tests, smoke test every page.

**Migrations are mandatory (product owner):** generate `InitialCreate` in `Servora.Persistence.Sqlite`,
`.SqlServer` and `.PostgreSql`; switch every environment from `EnsureCreated` to migrations; add a unit test per
provider that fails when the model has pending changes (`HasPendingModelChanges`) and one that applies all migrations
to an empty SQLite database; verify update path: install old build → create data → update → migrations applied
automatically with pre-migration backup (T-15). Document "adding a migration" in `docs/development/`.

**Collected requests from hand-offs (process during integration):**
- Migrations must include: `sys_report`, `sys_dashboard_pin`, `sys_audit_event.Sequence` + indexes on `Sequence`,
  `ActorName` (T-09); `sys_directory_group_map`, `sys_directory_sync_run` (T-02); `kb_*`, `cmdb_class`,
  `cmdb_rel_ci`, `alm_asset` (T-06); `OutboundEmail` (T-03); all T-05 tables.
- Navigation: Knowledge (`/knowledge`, `/knowledge/articles`, `/knowledge/admin`), CMDB (`/cmdb`, `/cmdb/map`,
  `/cmdb/classes`), Assets (`/assets`) (T-06); optional "New request" `/requests/new` (T-05), "Mail outbox" (T-03).
- Layout: wire `IBrandingService` into NavMenu/AuthLayout/titles, optional login banner, hide splash in AuthLayout
  (T-03); apply saved `User.Theme` after sign-in (T-02); My Work approvals count via
  `IApprovalService.CountPendingForUserAsync` (T-05).
- Remove `Components/KnowledgeCmdb/ModuleStyles.razor` and its usages (T-06).
- Add `<NotificationBell />` to TopBar; nav entries `/admin/tables` and `/notifications`; embed `CustomFieldsSection`
  and `SlaTimersCard` into typed forms; expose `Servora:Platform:PublicUrl` in settings (T-07).
- Default role grants for `api.access`, `mcp.access`, `ai.assistant.use`; make `PermissionCatalog.IsKnown("table.*")`
  consistent (T-10).
- API paths must answer JSON 401/403 instead of HTML redirects (e.g. `/api/v1/me` with a cookie session that still
  has to change its password); unauthenticated JSON POST to `/api/v1/table` returned 400 content-type (T-07 report).
- Migrations must also include the T-07 entities (CustomTable … TaskSla) and the 9 T-10 tables.
- Migrations must also include the 10 T-08 tables (`automation_*`, `int_*`).
- Portal (T-11): redirect `portal.access`-only users from `/` to `/portal` (`Home.razor`); nav entry
  `/portal/admin/announcements` (`portal.manage`); consider `portal.manage` for `config_admin`; let knowledge services
  accept `portal.access` to remove the duplicated portal search; `portal_announcement` migration.
- Kernel / T-14: record-level authorization for `IAttachmentService` and `/attachments/{id}` (portal already uses its
  own checked endpoint).
- Backup (T-15): generated migrations must keep designer files (`TargetModel`); run one backup → restore round trip
  on SQL Server and PostgreSQL; verify the update path with a real migration (pre-migration backup + auto-restore on a
  failing migration); consider raising the installer `--health-timeout` default; optionally move `AddBackupWeb()` into
  `WebModuleRegistration`.
- Kernel: instance-specific auth/antiforgery cookie names, or a friendly redirect on antiforgery failure, so several
  localhost instances do not clash (T-05).

**Result (validated by orchestrator; merges `c7b0396` T-13a, `22e769a` T-13b, follow-up `721e3ff`):**
- *Migrations (T-13a):* `InitialCreate` per provider (71 tables, designer files + snapshot kept); `EnsureCreated` removed
  from startup, the migration runner and the setup wizard (setup now refuses databases with tables but no migration
  history). Tests: pending model changes per provider, apply-all on empty SQLite with schema-vs-model check, startup
  path, legacy DB refusal, setup into empty / foreign DB. `docs/development/migrations.md` added.
- *Integration (T-13b):* navigation entries, branding in layouts/titles, login banner, saved theme after sign-in,
  notification bell, My Work approvals count, custom fields + SLA cards in typed forms, `ModuleStyles` removed,
  default role grants (once per role/permission, new `integration_user` role), strict `PermissionCatalog.IsKnown`,
  portal landing redirect, JSON problem responses (401/403/404) for `/api`, `/mcp`, `/openapi`, instance-specific
  cookie names, `AddBackupWeb()` in `WebModuleRegistration`.

**Evidence:** merged main build 0 errors / 0 warnings, 1390 tests passed. Smoke on fresh SQLite: schema created by
`20261108021317_InitialCreate`, `/health` 200, `/api/v1/me` and unauthenticated JSON POST → 401
`application/problem+json`, `/incidents` → 302 login, antiforgery cookie `servora.af.<hash>`. T-13a evidence: SQL Server
(LocalDB) migrate + `/health` + CLI backup/verify/restore round trip with 0 differences in 71 tables; SQLite update path
with a real second migration (pre-migration backup, data intact) and a failing migration (auto-restore, non-zero exit,
data intact). T-13b evidence: branding, banner, theme, bell, custom field save, portal redirect, API errors verified in
the running app.

**Open follow-ups:**
- PostgreSQL not verified against a real server (none installed, Docker Desktop not running): one migrate + backup
  round trip still required.
- Knowledge services accepting `portal.access` not done (would widen visibility; portal keeps its own filtered search).
- Custom field values are saved in a second write; with `create` but without `write` permission they fail (toast).
- Field index race warning between record service and `RecordFieldIndexHandler` (self-repairing) – T-14.
- Record-level authorization for attachments – T-14.
- Existing development databases created with `EnsureCreated` no longer start (delete or baseline, see `upgrade.md`).
- Every user signs in once after the upgrade (new cookie names).

## D-1 Design system redesign ✅ / P-1 Portal & catalog redesign ✅

**Result (validated by orchestrator; merges `d3418a9` D-1, `61f86e6` P-1):** light forest-green design from the product
owner's reference images; all colours derive from the configurable brand colour (`--primary`, `color-mix`/`oklch`),
legacy default accent `#7c6cff` maps to the new default `#1b4535`, dark theme optional. Portal shell, home and catalog
item configuration page rebuilt to the references (live price summary, server-side price calculation on order),
portal settings page (`/portal/admin/settings`, `portal.manage`), catalog model extensions + sections + favourites +
item images with migration `AddCatalogPortalExperience` (all providers), showcase catalog seed.
**Evidence:** merged main build 0/0, 1450 tests passed; screenshots of sign-in, My Work, incident list (D-1 build) and
portal home + business laptop page (merged build) match the references; the product owner's dev instance was upgraded
automatically (pre-migration backup, then `AddCatalogPortalExperience` applied).
**Open follow-ups:** no stock photos (SVG/icon placeholders until images are uploaded); UI copy in English (German
localization would be a separate task); no separate header-colour setting; "save as draft" not available; header
open-items badge refreshes on navigation; portal settings use a narrow elevation to `admin.settings.manage` (review in
T-14); `Pages/Catalog/*` agent-side builder pages still use `#fff` on primary (weak contrast in dark theme).

## T-14 Security & Architecture Review ⏸

Review against spec §14.2 gates and ORCHESTRATION.md §4; fix critical findings; record residual risks.

## T-15 Backup & Restore, automatic migrations hardening ✅

**Owned paths:** `*/Backup/**` (Domain/Application/Infrastructure/Web pages `Components/Pages/Admin/Backup/**` and
`Components/Backup/**`), `BackupModule.cs`, `BackupEndpoints.cs`, `BackupWebModule.cs`, `Permissions.Backup.cs`,
`tests/Servora.Tests.Unit/Backup/**`, `docs/modules/backup.md`, `docs/operations/backup-restore.md`.
**Kernel files granted to this task (orchestrator approval):** `Persistence/DatabaseInitializer.cs`,
`Modules/ServoraInfrastructureExtensions.cs`, `Endpoints/EndpointRegistration.cs`, `Program.cs` (CLI commands +
web module hook only), `NavigationCatalog.cs` (one admin entry), `docs/operations/upgrade.md`.
Raw SQL is allowed **only** inside `Servora.Infrastructure/Backup/**` for the generic dump/restore (identifiers
quoted via `ISqlGenerationHelper`, all values as parameters) – see ADR-015.

**Scope**
1. **Backup archive** (`.svbak`, zip): `manifest.json` (format version, app version, provider, last applied
   migration, created at/by, kind `Scheduled|Manual|PreMigration|PreRestore|Uploaded`, table list with columns,
   primary keys and foreign keys, row counts, SHA-256 per entry) + database payload + data-dir files
   (`keys/`, `attachments/` (optional), branding/uploads). Never `database.json` connection secrets in clear text;
   never the instance `appsettings.json` unless configured.
2. **Provider-neutral logical dump** of every table of the EF relational model (SQLite, SQL Server, PostgreSQL),
   streamed (no full table in memory), typed value encoding (null, string, integer, real, decimal, bool, bytes,
   guid, date/time types). Optional AES-256-GCM encryption of the archive with a password (PBKDF2-SHA256,
   `System.Security.Cryptography`, password stored in `ISecretStore`).
3. **Restore / roll back**: only into the same provider; refused when the backup's migration is newer than the
   app's. Always creates a `PreRestore` safety backup first. Executed at startup (restore request file in the data
   dir → app restarts → restore runs before migrations): migrate to the backup's migration (or wipe when no
   migrations exist yet), delete all rows, insert in FK order (nullable FKs in a second pass – works without
   superuser rights), then normal startup migrates to the latest version. Restores `keys/` and attachments.
   UI shows a restoring screen until `/health` answers again.
4. **Schedule** (stored in `<data dir>/backup.json`, survives restores): enabled, frequency daily/weekly/monthly,
   weekdays / day of month, time of day, time zone, destination folder (default `<data dir>/backups/instance`,
   UNC allowed), include attachments, encryption on/off, retention (keep last N scheduled, max age days, keep last
   N pre-migration), catch-up of a missed run after downtime. Hosted service with testable next-run calculation
   (`TimeProvider`), one backup/restore at a time, failures logged + audited + visible in the UI.
5. **Automatic migrations hardening** (`DatabaseInitializer`): pending migrations on an existing database ⇒
   `PreMigration` backup first (configurable, default on); migration failure ⇒ automatic restore of that backup and
   non-zero exit (installer health check then rolls back the binaries, so binaries and schema stay consistent);
   clear log messages; legacy `EnsureCreated` database detection with an actionable error.
6. **UI** `/admin/backups` (premium design system): KPIs (last successful backup, next run, count, total size),
   schedule editor, backup list (kind, date, version, migration, size, status) with download, verify (hashes),
   restore (confirm by typing `RESTORE`), delete, "Back up now", upload of a backup file (size limit), database
   schema card (current migration, applied / pending migrations).
7. **Command line** for disaster recovery when the UI does not start: `Servora.Web backup list|create|restore <file>`
   (and documented `--data-dir`).
8. **Permissions** `backup.read`, `backup.manage`, `backup.restore` (admin role only by default); audit every action.

**Acceptance criteria:** SQLite round trip test (seed data → backup → change data → restore → data equal incl.
nullable self-references); migrate-down/up restore path tested with a fake two-migration model or documented if not
feasible; schedule calculation unit tests (weekly at 02:00 local, DST change, missed run catch-up); retention tests;
archive tamper detection test; encryption round trip; `docs/operations/backup-restore.md` and `upgrade.md` updated;
build green, tests pass.

**Result (validated by orchestrator, merged `5a02f35`):** `.svbak` archive (manifest with schema of the migration the DB is at, streamed typed dumps, keys, attachments, branding; SHA-256 per entry; optional AES-256-GCM + HMAC-signed manifest), restore at startup before migrations (migrate to backup migration → replace rows in FK order with second pass for cycles → files → migrate forward; automatic rollback to the safety backup on failure; retry once then park), `DatabaseMigrationRunner` (pre-migration backup, auto-restore on failed migration, legacy / newer DB detection), scheduler in `backup.json` (daily/weekly/monthly, DST-safe, catch-up, retention), `/admin/backups`, CLI `Servora backup list|create|verify|restore`.
**Evidence:** files only in owned + granted paths; kernel diffs reviewed; build 0/0; tests 331 passed (98 new). Merged main: build 0/0, **1264 tests passed**. End-to-end by the orchestrator on the full merged schema (SQLite, 70 tables, 469 seeded rows): CLI create → verify → damage data (deleted 58 rows, renamed 10 users) → CLI restore (pre-restore safety backup created) → **all 70 tables content-identical** (per-table hash) → host starts healthy on the restored DB; in the browser `/admin/backups` shows restore status, list, KPIs and schema card; schedule set to weekly Sunday 02:00 (Europe/Berlin), persisted in `backup.json`, next run Sun 08 Nov 02:00; "Back up now" created an archive.
**Open follow-ups:** SQL Server / PostgreSQL paths only compiled + unit-tested on SQLite → one round trip per provider in T-13; visual review in both themes not done (browser pane hidden); form inputs in the schedule drawer lack label/aria association (T-14 a11y); UI restore restarts via exit code 75 (service recovery after ~1 min; manual restart in development).

---

## Change log of this board

| Date | Change |
|------|--------|
| 2026-11-07 | Board created; T-01 completed by orchestrator; T-02…T-10, T-12 dispatched in parallel. |
| 2026-11-07 | T-12 validated and merged (✅). |
| 2026-11-07 | T-04 validated and merged (✅); kernel UI fixes + 2 contract extensions. |
| 2026-11-07 | Product owner: automatic migrations + built-in backup/restore made binding; T-15 added and dispatched; interrupted T-02, T-03, T-05…T-10 resumed. |
| 2026-11-07 | T-02, T-03, T-05, T-06, T-09 validated and merged (✅); integration requests collected under T-13. |
| 2026-11-07 | T-11 dispatched (dependencies merged). |
| 2026-11-07 | T-07, T-10 validated and merged (✅). |
| 2026-11-07 | T-08 validated and merged (✅). |
| 2026-11-07 | T-15 validated (incl. orchestrator end-to-end backup/restore on the full schema) and merged (✅). |
| 2026-11-07 | T-11 validated and merged (✅). All implementation tasks done; next: T-13 integration. |
| 2026-11-08 | T-13 started: T-13a migrations and T-13b cross-module integration in parallel. |
| 2026-11-08 | T-13 validated and merged (✅); PostgreSQL real-server verification open. |
| 2026-11-08 | T-14 started (4 read-only reviews) and paused on product owner request; no findings recorded yet. |
| 2026-11-08 | D-1 and P-1 started in parallel (product owner: redesign to match reference images; colour, logo etc. stay configurable in the admin centre). |
| 2026-11-08 | D-1 and P-1 validated and merged (✅). |
