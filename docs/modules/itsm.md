# Incident & Problem Management + My Work (T-04)

Incident management restores normal service as quickly as possible; problem management finds and removes the
underlying causes. The module also provides the personalised **My Work** workspace at `/`.

## Tables

| Table | Entity | Number | Notes |
|-------|--------|--------|-------|
| `incident` | `Incident : TaskRecord` | `INC0001001` | Caller, category/subcategory, channel, business service (CI), major incident flag, parent incident, problem link, on-hold reason, resolution code + notes, reopen/reassignment counters, resolved at/by. |
| `problem` | `Problem : TaskRecord` | `PRB0001001` | Category, business service, known error flag, workaround, root cause, fix notes, resolution code, resolved at/by. Related incidents via `incident.ProblemId`. |
| `incident_category` | `IncidentCategory` | – | Categories (top level) and subcategories (`ParentId`). Records store the *name* as text. |

All three carry `[RecordTable]`, so they are visible to the table registry, business rules, flows, SLAs, reports and
the Table API. EF mappings live in `Infrastructure/Persistence/Configurations/Itsm` (`ConfigureTaskRecord` for task
tables, max lengths and indexes on every lookup column).

## State machines

```
Incident:  New ──► In Progress ◄──► On Hold
            │          │               │
            └──────────┴──► Resolved ◄─┘ ──► Closed
                               └──► In Progress (reopen, ReopenCount++)
           any open state ──► Cancelled

Problem:   New ──► Assess ──► Root Cause Analysis ◄──► Fix in Progress ──► Resolved ──► Closed
            └─────────────────────►┘          └──────────────────────────►┘   └──► Fix in Progress (reopen)
           any open state ──► Cancelled
```

Mandatory fields per state (`IncidentStateMachine` / `ProblemStateMachine` in the domain):

* Incident **On Hold** → on-hold reason; **Resolved/Closed** → resolution code **and** resolution notes.
* Problem **Fix in Progress** → root cause; **Resolved/Closed** → resolution code **and** fix notes; **known error** →
  workaround.

`Active` is `false` only for Closed/Cancelled (resolved records stay active until closed so they can be reopened).

## Invariants enforced for every writer – `ItsmSavingHook`

The `IRecordSavingHook` (order 10) runs inside `SaveChangesAsync` for all writers (services, Table API, flows,
imports, seeders):

1. **Priority** = `PriorityMatrix.Calculate(Impact, Urgency)` on insert and whenever impact, urgency or priority change.
2. **Transitions** are validated against the state machine (old value from the pending change) including the
   mandatory fields; invalid saves are aborted with a user-safe message (`RecordSaveAbortedException`).
3. `Active`, `ResolvedAt`, `ClosedAt` and `OpenedAt` are maintained; the on-hold reason is cleared outside On Hold.

## Application services

| Contract | Highlights |
|----------|-----------|
| `IIncidentService` | List (quick filters, server-side paging/sort/search incl. caller/assignee names), stats (open, P1, unassigned, SLA breached, major, 7-day trends), get, create, update (optimistic concurrency via `Version`, field-change journal), start work, put on hold, resolve, close, reopen, cancel, assign to me, bulk assign (≤ 200), link problem, children, incidents of a problem, search for pickers, delete. |
| `IProblemService` | List, stats, get, create, **create from incident** (copies summary, description, category, impact, urgency, CI, business service, group and links the incident), update, move to Assess/RCA/Fix, resolve, close, reopen, cancel, assign to me, bulk assign, link/unlink incidents, search, delete. |
| `IMyWorkService` | Assigned-to-me counts, groups' unassigned queue, P1/major counts, items assigned to me (incidents + problems), recent activity on my records, 14-day opened vs resolved trend, open incidents by priority. |

Every method checks permissions (`incident.read|create|write|delete`, `problem.read|create|write|delete`; linking
requires write on both sides) and returns `Result`/`Result<T>`. All queries are bounded and provider neutral (no raw
SQL, no ordering on decimals; trends are bucketed in memory from bounded timestamp lists).

State transitions, assignments and links write **system** activity entries; updates write **field change** entries
(with display values for users, groups, CIs and the recalculated priority) in the same unit of work.

## UI

| Route | Page |
|-------|------|
| `/` | My Work: greeting hero, KPI cards (assigned to me, groups' queue, critical incidents, approvals), my work list, recent activity, opened vs resolved chart (14 days), open incidents by priority. |
| `/incidents` | KPI row (clickable), quick filters (All active, My open, Unassigned, P1/P2, My groups, Major, Resolved, All – also via `?filter=my|unassigned|high|groups|major|resolved|all`), searchable/sortable/paged table, multi-select bulk assign / assign to me. |
| `/incidents/new`, `/incidents/{id}` | Header actions by state (Assign to me, Start work/Resume, Put on hold, Resolve, Reopen, Close, Save, more: Create problem, Cancel, Delete), `StateFlow`, two-column form (summary, classification, prioritization with live priority, assignment), side cards (SLA timers, caller card, key facts), tabs Activity / Related (problem link + child incidents) / Attachments. New incidents show an interactive priority matrix. |
| `/problems` | KPI row, quick filters (`?filter=my|unassigned|high|known|resolved|all`), table with linked incident counts, bulk assign. |
| `/problems/new`, `/problems/{id}` | Lifecycle actions (Assess, Start RCA, Start fix, Resolve, Reopen, Close, Cancel, Delete), analysis section (root cause, workaround, known error, fix notes), impact card, coordinator card, tabs Activity / Incidents (link/unlink) / Attachments. |

State actions are disabled while the form has unsaved changes. Dialogs: `ActionDialog` (choice + text, used for
resolve/on hold/reopen/cancel), `BulkAssignDialog`, kernel `ConfirmDialog` for create-problem and delete.

## Seed data (`ItsmDataSeeder`, order 500, idempotent)

* 7 categories with subcategories (only when the category table is empty).
* 25 realistic incidents in all states (over the last 14 days) and 4 problems (RCA with known error, fix in progress,
  resolved, assess) with links, a parent/child pair, work notes and resolution entries – only when both incident and
  problem tables are empty.
* Work is assigned to the bootstrap `admin` so My Work is meaningful after installation; callers are taken from
  existing active users; groups "Service Desk", "Network Operations", "Database Administration", "Application
  Support" are used by name (created if Identity has not seeded them); CIs are linked when the CMDB seeded them.

## Known limitations

* `ISlaService` only exposes timers per record, so the "SLA breached" KPI samples the 100 most recently updated
  active incidents.
* `IApprovalService` has no per-user pending count yet; the My Work approvals card shows "—" and links to `/approvals`.
* Deleting a record removes its activity entries but not its attachments (no bulk delete in `IAttachmentService`).
