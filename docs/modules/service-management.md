# Change, Catalog, Requests & Approvals (T-05)

Service management covers three connected processes:

* **Change management**: plan, assess, authorize (CAB) and implement changes to services and configuration items.
* **Service catalog and requests**: catalog managers design orderable items with questions; employees (portal) or
  fulfillers order them; fulfilment teams work catalog tasks until the request closes automatically.
* **Approvals engine**: a generic engine behind `IApprovalService`, used by changes, requested items and any other
  module (flows, custom tables).

## Tables

| Table | Entity | Number | Notes |
|-------|--------|--------|-------|
| `change_request` | `ChangeRequest : TaskRecord` | `CHG0001001` | Type (Normal / Standard / Emergency), state, category, requested by, justification, implementation / backout / test plans, risk + score + questionnaire answers (JSON), planned and actual start/end, approval group, approval status (denormalized), close code + notes. |
| `change_task` | `ChangeTask : TaskRecord` | `CTASK0001001` | Tasks of a change (state Open / In progress / Completed / Cancelled, planned window, order, close notes). |
| `sc_category` | `CatalogCategory` | – | Name, description, icon, order, active. |
| `sc_cat_item` | `CatalogItem` | – | Name, short/long description, icon, category, price, **billing type (one-time / monthly), term months, one-time costs**, delivery days + **delivery text**, fulfilment group, fulfilment task template (one task per line), approval required + approval group + manager approval + policy (any/all), order, active; portal presentation (P-1): **subtitle, badge, service ID (`Code`), image id, teaser + teaser text, highlights (`icon \| text` lines), details / delivery Markdown, featured flag + featured order**. |
| `item_option_new` | `CatalogVariable` | – | Variable definitions of an item: name, label, type (text, multiline, number, choice, boolean, date, user reference), mandatory, order, help text, default, choice options (optionally with price impact and description, see below), min/max, max length, **section** (`SectionId`), **price impact of a checked yes / no question** (`PriceDelta`). |
| `sc_item_section` | `CatalogSection` | – | Numbered order form section of an item (title, description, order). Variables reference it; deleting a section moves its variables to the general section. |
| `sc_favorite` | `CatalogFavorite` | – | A user's favourite catalog item (unique per user and item). |
| `sc_request` | `Request` | `REQ0001001` | The order (requested for, ordered by, state Submitted / In progress / Completed / Rejected / Cancelled). |
| `sc_req_item` | `RequestItem` | `RITM0001001` | One ordered item: catalog item, quantity, unit price **computed server-side incl. option impacts**, billing type, term, one-time costs, variable answers (JSON), state Pending approval / In fulfilment / Completed / Rejected / Cancelled, approval status. |
| `sc_task` | `CatalogTask : TaskRecord` | `SCTASK0001001` | Fulfilment task of a requested item. |
| `sys_approval` | `Approval` | – | One approver's approval of any record (see below). |

All tables carry `[RecordTable]` (table registry, business rules, flows, reports, Table API). EF mappings live in
`Infrastructure/Persistence/Configurations/{Changes,Catalog,Approvals}` with max lengths on every string and indexes
on lookup columns. Decimal prices are never used for ordering in SQL (SQLite limitation).

## Approvals engine

* `IApprovalService` (kernel contract, implemented by `ApprovalService`): `RequestAsync`, `GetSummaryAsync`,
  `CancelAsync`, `CountPendingForUserAsync` (My Work KPI).
* `IApprovalInboxService`: approver inbox (`/approvals`), statistics, approve / reject (comment mandatory for
  rejections), approvals of a record (approvals card on change and request pages).
* `ApprovalEngine` (internal workhorse, no permission checks; only called by authorized services):
  * **Group expansion**: direct approvers plus every active member of the (active) approval groups, de-duplicated,
    max 200 approvers / 25 groups per round. Rows remember the group the approver came from ("via CAB").
  * **Rounds**: all rows of one request share a `BatchId`. A new request withdraws pending rows of earlier rounds.
  * **Policies** (`ApprovalPolicyEvaluator`): *any approver* – the first decision decides the round; *all approvers*
    – every approver must approve, the first rejection rejects. Remaining open rows become `NotRequired`.
  * **Decisions** are bound to the approver; users with `approval.manage` may decide on behalf (recorded in the
    comment). Concurrent decisions are protected by optimistic concurrency.
  * **Notifications**: approvers are e-mailed through `IEmailSender` (best effort, HTML-encoded content); a system
    entry is written to the record's activity stream.
* **Extensibility**: `IApprovalRecordHandler` connects a table to the engine (describe the record for the inbox, react
  when a round is decided). Built-in handlers: `ChangeApprovalHandler`, `RequestItemApprovalHandler`. Other modules can
  register their own handler or react to `sys_approval` record events.

## Change management

```
Normal / Emergency:  New ──► Assess ──► Authorize ──► Scheduled ──► Implement ──► Review ──► Closed
                      ▲        │  ▲         │
                      └────────┘  └─────────┘ (CAB rejected → Assess)
Standard (pre-approved): New ──► Scheduled ──► Implement ──► Review ──► Closed
Cancel: from any state before Implement.
```

`ChangeStateMachine` (pure logic, unit tested) validates transitions and the mandatory data of the target state:

* **Authorize**: planned window (start < end), implementation plan, backout plan; normal changes also need a risk
  assessment.
* **Scheduled**: standard changes need a planned window and an implementation plan; normal/emergency changes need an
  **approved** CAB round (normally set automatically by the approval handler).
* **Closed**: close code + close notes and no open change tasks.

Entering **Authorize** requests approval from the change's approval group or, by default, the group
**"Change Advisory Board"** (seeded). Normal changes with risk High/Very high require *all* CAB members, everything
else *any* member. Approved → `Scheduled`, rejected → back to `Assess`; leaving Authorize otherwise withdraws pending
approvals.

**Risk assessment** (`ChangeRiskCalculator`): six questions (affected users, outage, testing, backout, complexity,
experience) → score → Low (≤ 3) / Moderate (≤ 7) / High (≤ 11) / Very high.

**Conflict detection**: changes on the same CI whose planned windows overlap (open changes only) are listed on the
record page; `IChangeService.FindConflictsAsync` offers the same check for planning tools (portal, flows, API).

**Invariants for every writer** – `ChangeSavingHook` (order 10) runs inside `SaveChangesAsync` for services, Table API,
flows and imports: priority from `PriorityMatrix`, state transitions validated by `ChangeStateMachine` (using the
record's denormalized approval status), `Active` / `ClosedAt` / `OpenedAt` maintained for changes and change tasks.
The open-task check before closing stays in `ChangeService` (needs other records).

**Change calendar** (`/changes/calendar`): month grid of planned windows coloured by type, KPIs for the month.

## Service catalog and requests

* **Builder** (`/catalog-admin`, `/catalog-admin/{id}`, permission `catalog.manage`): categories, items, variables
  (add, edit, reorder, delete), duplicate item, live portal-style preview with validation. Definitions are validated
  by `CatalogVariableValidator.ValidateDefinition` (unique technical names, choice options, ranges). Changes are audited.
* **Ordering** – `ICatalogOrderService` (used by the portal T-11 and by fulfillers on `/requests/new`):
  `GetCategoriesAsync`, `GetItemsAsync`, `GetOrderFormAsync`, `OrderAsync(itemId, variables, requestedFor, quantity)`.
  Answers are validated and normalized by `CatalogVariableValidator.Validate` (mandatory, type, choice options, ranges,
  length, active user references). Ordering for someone else requires `request.write`.
* **Pricing (P-1)** – an item has a base `Price` billed one-time or monthly (`BillingType`, `TermMonths`, one-time
  costs `OneTimePrice`). Choice options carry price impacts in a backwards-compatible line format stored in
  `CatalogVariable.Options`: `Label` (no impact, the format used before) or `Label | +8.00 | optional description`; a
  line whose second segment is not a number stays a plain label, so existing options containing `|` keep working
  (`CatalogChoiceOption`). Answers store only the label. A checked yes / no question adds its `PriceDelta` (optional
  accessories). `CatalogPriceCalculator` computes the quote (lines, unit price, total, one-time total, selected options);
  the portal shows it live and `OrderAsync` **always recomputes it from the stored definition** – prices sent by a
  client are never used. `Request.TotalPrice` = unit price × quantity (per month for monthly items). The well-known
  choice variable `cost_center` is shown separately in summaries.
* **Portal presentation (P-1)** – `ICatalogOrderService` additionally offers `QuoteAsync`, favourites
  (`GetFavoriteIdsAsync`, `SetFavoriteAsync`, users only) and `OpenImageAsync`; the order form carries sections,
  highlights, details / delivery Markdown (rendered with `SafeMarkdown`), the pricing, the approver to expect (manager of
  the requester, otherwise the approval group), favourite state and whether the user may order on behalf.
  `ICatalogAdminService` adds `SaveSectionAsync`, `DeleteSectionAsync`, `UploadItemImageAsync`, `RemoveItemImageAsync`
  (all `catalog.manage`, audited). Images are verified by magic bytes (PNG, JPEG, GIF, WebP – never SVG, max. 2 MB),
  stored as `<data dir>/catalog/images/<id>.bin` (`ICatalogImageStore`) and served by `GET /portal/catalog-images/{id}`
  (catalog browse permission; images of inactive items only for catalog managers; `nosniff`). Duplicating an item copies
  sections (remapped), presentation fields and a copy of the image.
* **Builder additions (P-1)** – pricing (billing, term, one-time costs), portal presentation (subtitle, service ID,
  badge, featured + order, teaser, highlights, image upload), order page tabs (Markdown), form sections (add / edit /
  delete), section and price impact per question; option help text explains the `Label | +8.00 | description` format.
  `/requests/new` shows the live price (incl. option impacts and term).
* **Workflow** (`CatalogFulfillmentEngine`):
  1. Order → `REQ` + `RITM` (answers JSON).
  2. Approval required → approval round (approval group and/or the requested-for user's manager, policy any/all);
     item state *Pending approval*. If no approver can be resolved the order is refused before anything is created.
  3. Approved (or no approval) → one `SCTASK` per line of the item's task template (default: one task) for the
     fulfilment group; item *In fulfilment*.
  4. Rejected → item and request *Rejected*.
  5. All tasks closed → item *Completed* (or *Cancelled* when every task was cancelled) → request state rolled up
     (`RequestStateCalculator`).
* **Roll-up for every writer** – `CatalogTaskEventHandler` (record event handler) re-runs the idempotent roll-up when a
  catalog task's state changes outside `IRequestService` (flows, Table API), so requests always close.
* **Fulfiller workspace** (`/requests`, `/requests/{id}`, `/requests/new`): KPIs, catalog tasks / requested items /
  requests views with quick filters, request record with answers, approvals, tasks (assign to me, complete, drawer
  for state/group/assignee/notes), cancel with reason, activity and attachments; order-on-behalf page with catalog
  browsing, search, category chips and the same order form/validation as the portal.

## Permissions

| Permission | Meaning |
|------------|---------|
| `change.read` / `change.create` / `change.write` / `change.approve` / `change.delete` | Change management. |
| `request.read` / `request.write` | View / fulfil requests, items and tasks; order on behalf of others. |
| `catalog.manage` | Catalog builder. |
| `approval.read` / `approval.manage` | See all approvals / decide on behalf, request and cancel approvals of any table. |
| `portal.access` | Browse the catalog and order for oneself (portal). |

Every application service method checks permissions server-side and returns `Result.Forbidden()`; pages carry
`[RequirePermission]` (the approvals inbox is open to every signed-in user and only returns the user's own approvals
unless `approval.read` is granted).

## Seed data (idempotent)

* `ChangeManagementSeeder` (order 110): groups *Change Advisory Board* (approval, admin as member), *Network
  Operations*, *Database Administration*, *Desktop Support*; six demo changes in different states (one awaiting CAB
  with a pending approval round).
* `CatalogSeeder` (order 120): approval group *IT Approvers*; categories *Hardware*, *Software*, *Access &
  permissions* (an existing *Access & Security* category is reused), *IT support*, *Communication*, *People &
  Onboarding*; base items matched by name and only created when missing (Software installation, Access request, VPN
  access, Mobile phone, Password reset, New employee onboarding, Shared mailbox). The **portal showcase** is added once
  (marker setting `catalog.showcase.seeded`, so deleted items are not recreated): *Business laptop* (Lenovo ThinkPad T14
  Gen 5, €39 / month, 36 months, three sections, 32 GB RAM +€8, 1 TB SSD +€4, USB-C dock +€5, cost centre, delivery
  location, desired date, justification, return-device confirmation, manager + IT Approvers approval), *Microsoft 365*
  (€22 / month, E5 +€16), the portal presentation of *VPN access* (only while its new presentation fields are still
  empty) and a realistic range (24 hardware services in total, 11 software, 8 access, 6 IT support). Existing data is
  never overwritten. Before P-1 *New laptop* was seeded; new installations no longer get it, existing ones keep it.

## Tests

`tests/Servora.Tests.Unit/Catalog` (P-1): option line format and backwards compatibility (`CatalogChoiceOptionTests`),
server-side price calculation (`CatalogPriceCalculatorTests`), ordering stores the server-computed price and ignores
client price fields, order form sections / approver / requester, favourites, authorization of the new methods, section
deletion, image type checks and serving rules, duplicate (`CatalogPortalServiceTests`), seeder showcase, idempotency,
no overwriting of administrator changes and the legacy access category (`CatalogSeederTests`).

`tests/Servora.Tests.Unit/ServiceManagement`: approval policy evaluator, approval engine (group expansion, any/all,
on-behalf decisions, rounds, permission checks), change state machine, change lifecycle with CAB approval/rejection and
conflicts, change saving hook, catalog variable validation, order → approval → tasks → closure flows, roll-up through
the catalog task event handler, request state calculator. Service tests run on in-memory SQLite with the real DI graph
of `ServiceManagementModule`.
