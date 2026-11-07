# Architecture Decision Records

Short ADRs. Status: Accepted unless noted. Newest last.

## ADR-001 – Modular monolith on .NET 10
**Decision:** One deployable ASP.NET Core process (Blazor UI + REST + MCP + background services) built as a modular
monolith with strict layering: Domain → Application → Infrastructure → Web. Target framework `net10.0` (current LTS).
**Why:** Spec §1.1/§3 (no premature microservices); simplest self-hosted operations (single Windows service).

## ADR-002 – Blazor Web App, Interactive Server, no prerendering
**Decision:** Global `InteractiveServer` render mode with `prerender: false`; static SSR only for pages that must
write cookies (login, logout, password change, error) via `[ExcludeFromInteractiveRouting]`.
**Why:** Rich enterprise UI with direct server-side service access, no public API surface needed for the UI, one
execution per component (simpler, no double data loads), works well on intranet latency. A splash screen covers
circuit start-up.

## ADR-003 – Custom design system instead of a component library
**Decision:** Hand-crafted design system (`wwwroot/css/servora.css`, tokens, dark/light themes) and in-house Razor
components including SVG charts and an icon set drawn for Servora.
**Why:** Product owner demands a distinctive premium look; avoids dependency/licensing risk; full control.

## ADR-004 – Application layer uses EF Core abstractions directly
**Decision:** `Servora.Application` references `Microsoft.EntityFrameworkCore` (provider-agnostic) and works with
`IAppDbContext.Set<T>()`; providers live in `Servora.Persistence.*`.
**Why:** Avoids a repository layer that duplicates EF; keeps business logic testable with SQLite in-memory; providers
remain isolated (spec §4.2).

## ADR-005 – Task records: shared base class, one table per type
**Decision:** `TaskRecord` is a C#-only base class; every task type (incident, problem, change, request item, …) is
mapped to its own table. Cross-type "my work" views union in the application layer.
**Why:** Avoids huge sparse TPH tables and column-sharing issues across three providers; per-type state enums stay
strongly typed.

## ADR-006 – Bootstrap administrator `admin` / `admin` with mandatory password change
**Decision:** The identity seeder creates `admin` with password `admin` and `MustChangePassword = true`. Until the
password is changed, every request is redirected to the change-password page (API calls get 403); the password
policy rejects `admin` and passwords containing the user name. The event is logged as a warning and audited.
**Why:** Product owner requirement ("admin/admin at start") reconciled with spec §4.3/§5.1 ("never ship a universal
default password … require password change"). The default password is only valid for the very first sign-in.

## ADR-007 – Secret protection with ASP.NET Core Data Protection
**Decision:** `ISecretStore` encrypts values with Data Protection; key ring persisted to `<data dir>/keys`, protected
with machine-level DPAPI on Windows. The database connection string in `database.json` is protected the same way.
**Why:** Established .NET cryptography (no custom crypto, spec §7.2); keys outside the database. Losing the key ring
makes stored secrets unrecoverable – documented in operations/backup.

## ADR-008 – Record events and before-save hooks in the DbContext
**Decision:** `ServoraDbContext.SaveChangesAsync` detects changes on `[RecordTable]` entities, runs
`IRecordSavingHook`s (synchronous "before" rules, may abort), commits, then publishes `RecordChangedEvent`s to a bounded
in-process channel processed by `RecordEventProcessor` (async rules, flows, SLAs, notifications). Cascade depth is
limited to 5.
**Why:** One consistent extension point for business rules, flows, SLAs and notifications across all tables without
modules knowing each other.

## ADR-009 – Hybrid data model: typed tables + custom tables/fields as JSON
**Decision:** Core ITSM entities are typed EF entities; administrator-defined custom fields are stored in
`CustomFieldsJson`, custom tables in a generic record table. The table registry merges both for generic features.
**Why:** Strong typing and performance for core modules plus UI-only extensibility (product owner: configuration only
via UI) without runtime schema migrations on three providers.

## ADR-010 – Dependency policy
**Decision:** Only Microsoft / OSI-licensed packages (MIT, Apache-2.0, PostgreSQL). Current set: EF Core (+ SQLite, SQL
Server, Npgsql), Windows Services hosting, Negotiate authentication, OpenAPI, System.DirectoryServices.Protocols,
xUnit. Vulnerable transitive packages are pinned to patched versions (central transitive pinning). No UI, charting,
logging or MCP SDK packages – implemented in-house.
**Why:** Product owner: no licensed packages, open-source project; minimal supply-chain surface.

## ADR-011 – Data directory and configuration
**Decision:** Runtime data (database.json, SQLite DB, keys, logs, attachments, instance `appsettings.json`) lives in
`%ProgramData%\Servora` for the service, `./App_Data` in development, overridable by `SERVORA_DATA_DIR`. Program files
can be replaced on upgrade without touching data.
**Why:** Safe in-place upgrades by the installer (T-12) and clear backup scope.

## ADR-012 – MCP over Streamable HTTP, implemented in-house
**Decision:** The MCP server is a JSON-RPC 2.0 endpoint at `/mcp` (Streamable HTTP transport) implemented without an SDK,
authenticated with API keys and scoped per tool.
**Why:** Avoids pre-release SDK dependencies; MCP is an adapter over application services (spec §10.1).

## ADR-013 – Parallel development with kernel placeholders
**Decision:** Cross-module contracts are defined in the kernel with placeholder implementations; owning modules
register the real implementation later (last registration wins). Tasks run in isolated git worktrees and own disjoint
folders.
**Why:** Enables parallel sub-agents without merge conflicts or blocking dependencies.

## ADR-014 – Migrations generated at integration time
**Decision:** During parallel development the schema is created with `EnsureCreated` (development databases are
disposable). In T-13 the orchestrator generates `InitialCreate` migrations per provider; from then on
`DatabaseInitializer` applies migrations automatically.
**Why:** Avoids conflicting migration snapshots from parallel branches.
