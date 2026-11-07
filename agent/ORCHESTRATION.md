# Orchestration Protocol (Orchestrator ↔ Sub-Agents)

This document defines **how** Servora is built by an orchestrator that delegates bounded tasks to specialised
sub-agents. The **what** (task list, dependencies, status, results) lives in [`TASKS.md`](TASKS.md). Architecture
decisions live in [`DECISIONS.md`](DECISIONS.md). The product requirements are in [`../PROJECT_SPEC.md`](../PROJECT_SPEC.md)
and the product owner's instructions (summarised in TASKS.md › "Product owner directives").

---

## 1. Roles

| Role | Responsibilities |
|------|------------------|
| **Orchestrator** | Owns architecture, the kernel (`T-01`), contracts between modules and this protocol. Splits work into tasks, assigns them to sub-agents, **validates every result** against the acceptance criteria, merges branches, resolves integration issues and keeps `TASKS.md` truthful. Sends incomplete work back to the same sub-agent with concrete findings ("rework loop"). |
| **Sub-agent** | Implements exactly one task inside its file ownership boundary, writes tests and documentation, builds, runs tests, commits to its own branch and returns the hand-off report (section 6). Never edits files owned by another task. |
| **Review agent** | After integration: cross-cutting security and maintainability review (spec §14.2 gates). |

## 2. Lifecycle of a task

```
Planned ──► In Progress ──► Submitted ──► In Review ──► Done
                ▲                              │
                └──────── Rework (findings) ◄──┘
```

1. **Planned** – task described in TASKS.md with scope, ownership, dependencies and acceptance criteria.
2. **In Progress** – sub-agent spawned in its own git worktree / branch `task/T-xx-<slug>`.
3. **Submitted** – sub-agent returned its hand-off report and committed.
4. **In Review** – orchestrator inspects evidence: builds the branch, runs tests, reads the diff, checks acceptance
   criteria and the security gates. *A sub-agent's claim is never accepted without evidence.*
5. **Rework** – findings are sent back to the same sub-agent (with context intact) as a numbered list. Loop until clean.
6. **Done** – merged into `main`, solution builds with zero errors, tests pass, TASKS.md updated with result,
   changed areas and test evidence.

## 3. Ownership rules (avoid merge conflicts)

Each task owns **folders**, named after its module. A sub-agent may create and edit files only inside its owned
paths. Shared kernel files belong to the orchestrator (`T-01`).

| Layer | Owned path pattern (replace `<Module>`) |
|-------|------------------------------------------|
| Domain | `src/Servora.Domain/<Module>/**` |
| Application | `src/Servora.Application/<Module>/**` |
| Infrastructure | `src/Servora.Infrastructure/<Module>/**`, `src/Servora.Infrastructure/Persistence/Configurations/<Module>/**` |
| DI registration | `src/Servora.Infrastructure/Modules/<Module>Module.cs` (pre-wired stub) |
| Web pages | `src/Servora.Web/Components/Pages/<Module>/**` |
| Web components | `src/Servora.Web/Components/<Module>/**` (module-specific components) |
| Endpoints | `src/Servora.Web/Endpoints/<Module>Endpoints.cs` (pre-wired stub) |
| Host hooks | `src/Servora.Web/Modules/<Module>WebModule.cs` (pre-wired stub) |
| Permissions | `src/Servora.Application/Security/Permissions.<Area>.cs` of the task's area (add constants there) |
| Tests | `tests/Servora.Tests.Unit/<Module>/**` |
| Docs | `docs/modules/<module>.md` |

**Never edit** (orchestrator-owned): `Program.cs`, `App.razor`, `Routes.razor`, `_Imports.razor` files outside your
folder, `MainLayout`, `NavMenu`, `NavigationCatalog.cs`, `servora.css`, `Components/UI/**`, `CoreModule.cs`,
`ServoraDbContext.cs`, `Directory.*.props`, `.csproj` files, other tasks' folders. If you need a change there,
describe it in the hand-off report under **"Requests to orchestrator"**. Need a new CSS class? Put module-specific
styles in a scoped `*.razor.css` file next to your component.

Need a NuGet package? Don't add it – request it (only free OSI licenses are acceptable; see ADR-010).

## 4. Engineering rules (binding for every task)

1. **One type per file.** No nested classes/records/enums. File name = type name. (Razor components count as one type.)
2. **XML documentation in English** on every public and internal type and member (`/// <summary>`). Razor
   components: a one-line `@* ... *@` header comment plus doc comments on parameters.
3. **Clean architecture**: Domain has no dependencies; Application contains use cases/services + DTOs and talks to
   the database only through `IAppDbContextFactory`; Infrastructure contains external integrations; Web contains
   UI and endpoints only (no business rules in components).
4. **Data access**: `await using var db = await dbFactory.CreateDbContextAsync(ct);` per operation. Never keep a
   context in a component. Use `AsNoTracking()` for reads. Always bound queries (paging, `Take`).
5. **Authorization server-side**: every application service method that reads or mutates data checks
   `ICurrentUser.HasPermissionAsync(...)` and returns `Result.Forbidden()` otherwise. Pages additionally use
   `@attribute [RequirePermission(Permissions.X.Y)]`. The UI is never the security boundary.
6. **Results, not exceptions**, for expected failures (`Result`, `Result<T>`). Show them with
   `ToastService.FromResult(...)`.
7. **Audit** security/configuration relevant actions through `IAuditService` (never include secrets).
8. **Secrets** only through `ISecretStore`. Never log or return secrets; mark sensitive entity properties with
   `[NotExposed]`.
9. **Tables**: user-facing record entities derive from `AuditableEntity` (task-like ones from `TaskRecord`), carry
   `[RecordTable(...)]` and `[Reference(...)]` on foreign keys, and get an `IEntityTypeConfiguration<T>` in your
   `Persistence/Configurations/<Module>/` folder (max lengths on every string, indexes on lookups). Use
   `TaskRecordConfigurationExtensions.ConfigureTaskRecord` for task subtypes. Record events, audit fields and
   "before" hooks then work automatically.
10. **Provider neutrality**: no raw SQL, no provider-specific functions. Must work on SQLite, SQL Server and
    PostgreSQL. Do not `OrderBy` on `decimal` columns (SQLite limitation) – order by another column or in memory.
11. **No migrations** in tasks. The orchestrator generates provider migrations during integration (`T-13`).
12. **Seed data** through an idempotent `IDataSeeder` (Order ≥ 100) registered in your module file.
13. **Register services** in your `<Module>Module.cs` using `services.AddScoped<IContract, Impl>()` (the last
    registration wins over kernel placeholders – don't use `TryAdd` for contract implementations).
14. **UI quality bar**: use the design system (`docs/development/ui-guidelines.md`). Every list page = `PageHeader`
    + KPI `StatCard`s where meaningful + `DataTable` with search/sort/paging + empty state. Every record page =
    header with number/state, `StateFlow`, two-column form (`form-grid`) + side card with key facts, tabs for
    activity / related records / attachments. Loading skeletons, toasts for feedback, confirmation for destructive
    actions. It must look like premium enterprise software, not a scaffold.
15. **No raw arbitrary code execution, no arbitrary URL proxying, no generic SQL** anywhere (spec §6, §8, §10).
16. **Tests**: unit tests for domain/application logic you add (xUnit, `tests/Servora.Tests.Unit/<Module>/`).
    In-memory SQLite (`Data Source=:memory:` with an open connection) is fine for service tests.
17. **Build must stay green**: `dotnet build Servora.slnx` with zero errors and no new warnings in your files;
    `dotnet test` passes.

## 5. Kernel contracts available to every task

| Contract | Purpose | Implemented by |
|----------|---------|----------------|
| `IAppDbContextFactory` / `IAppDbContext` | Unit of work (EF Core) | T-01 |
| `ICurrentUser`, `ActorContext`, `ActorInfo` | Who is acting (user, API key, agent, system) | T-01 |
| `IPermissionService`, `PermissionMatcher`, `Permissions.*` | RBAC | T-01 (T-02 extends) |
| `IAuditService` | Hash-chained audit log | T-01 |
| `ISecretStore` | Encrypted credentials (Data Protection) | T-01 |
| `INumberSequenceService` | `INC0001001` style numbers | T-01 |
| `IActivityService`, `IAttachmentService` | Journal & files for any record | T-01 |
| `IDirectoryLookupService` | User/group/CI pickers | T-01 |
| `IRecordSavingHook`, `IRecordEventHandler`, `RecordChangedEvent` | Before-save hooks & async record events | T-01 |
| `ISignInService`, `IExternalCredentialValidator`, `PasswordPolicy` | Sign-in pipeline | T-01 (T-02 adds LDAP) |
| `ITableRegistry`, `IRecordService`, `IConditionEvaluator`, `ConditionGroup` | Generic tables / records / conditions | **T-07** (placeholder in kernel) |
| `ISlaService` | SLA timers of a record | **T-07** (placeholder) |
| `IEmailSender` | SMTP e-mail | **T-03** (placeholder) |
| `IApprovalService` | Generic approvals | **T-05** (placeholder) |
| `IFlowExecutionService` | Start/inspect flows | **T-08** (placeholder) |
| `IDataSourceExecutor` | Execute approved REST endpoints | **T-08** (placeholder) |

UI building blocks (see ui-guidelines): `PageHeader`, `StatCard`, `DataTable`/`DataColumn`, `Modal`, `Drawer`,
`Tabs`, `Switch`, `FormField`, `StatusPill`, `PriorityBadge`, `Avatar`, `RelativeTime`, `EmptyState`, `Skeleton`,
`ConfirmDialog`, `PermissionView`, `Icon`, charts (`LineChart`, `BarChart`, `DonutChart`, `Sparkline`), record
widgets (`UserPicker`, `GroupPicker`, `CiPicker`, `ReferencePicker`, `ActivityStream`, `AttachmentList`,
`ConditionBuilder`, `StateFlow`), `ToastService`.

## 6. Hand-off report (returned by every sub-agent)

```
## T-xx Hand-off
**Summary:** …
**Files changed:** (folders / key files)
**Build:** `dotnet build Servora.slnx` → result
**Tests:** `dotnet test` → passed/failed counts, new tests
**Acceptance criteria:** checklist with ✅ / ❌ per criterion
**Security notes:** authz checks, secrets, input validation, SSRF, …
**Known limitations / follow-ups:** …
**Requests to orchestrator:** shared-file changes, packages, nav entries, contract changes
**Commit:** branch + commit hash
```

## 7. Orchestrator validation checklist (per submission)

- [ ] Branch builds with zero errors; no new warnings in task files.
- [ ] `dotnet test` green; new logic has tests.
- [ ] Only owned paths changed (`git diff --stat main...`).
- [ ] One type per file; XML docs in English present.
- [ ] Every service method checks permissions; pages carry `[RequirePermission]`.
- [ ] No secrets in logs/responses; sensitive fields `[NotExposed]`.
- [ ] Queries bounded and provider-neutral.
- [ ] UI follows the design system (header, KPIs, tables, empty/loading states, toasts, confirmations).
- [ ] Acceptance criteria of the task met.
- [ ] Module documentation `docs/modules/<module>.md` written.

Any unchecked item ⇒ status **Rework** with numbered findings sent to the sub-agent.
