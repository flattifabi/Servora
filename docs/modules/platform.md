# Platform Engine (T-07)

The platform engine turns Servora from a set of typed ITSM modules into a configurable platform: every table –
built-in or created in the UI – can be listed, filtered, edited, automated with business rules, measured with SLAs,
notified about and accessed through one generic REST API. Everything is configured in the UI; there is no code
execution and no runtime schema change (ADR-009).

| Area | UI | Service contracts |
|------|----|-------------------|
| Table registry, custom tables & fields, layouts | `/admin/tables`, `/admin/tables/{name}` | `ITableRegistry`, `ITableDesignerService` |
| Generic records | `/table/{name}`, `/table/{name}/{id}`, `/table/{name}/new` | `IRecordService`, `IRecordLookupService`, `ITableAccessService` |
| Conditions | `ConditionBuilder` everywhere | `IConditionEvaluator`, `ConditionExpressionBuilder`, `ConditionValidator` |
| Business rules | `/admin/business-rules` | `IBusinessRuleService` |
| SLAs | `/admin/sla`, `SlaTimersCard` | `ISlaService`, `ISlaAdminService` |
| Notifications | `/admin/notifications`, `/notifications`, `NotificationBell` | `INotificationAdminService`, `INotificationDispatcher`, `INotificationInbox` |
| Record numbers | `/go/{number}` | `IRecordNumberResolver` |
| REST | `/api/v1/table/*`, `/api/v1/go/{number}`, `/api/v1/notifications/*` | `PlatformEndpoints` |

## Permissions

| Permission | Grants |
|------------|--------|
| `platform.records.read` | Use the generic list and form views (table permissions still apply). |
| `platform.tables.manage` | Create custom tables and fields, edit form layouts and list columns. |
| `platform.businessrules.manage` | Manage business rules. |
| `platform.sla.manage` | Manage SLA definitions and monitor timers. |
| `platform.notifications.manage` | Manage notifications and e-mail templates. |

**Table permissions.** Every record operation is authorised per table by `ITableAccessService`:

- Built-in tables map to the owning module's permissions (`TablePolicies`), e.g. `incident` → `incidents.read`,
  `incidents.create`, `incidents.write`, `incidents.delete`. Operations a module does not expose generically (for
  example creating users) are denied; `sys_user` / `sys_user_group` only allow writing profile fields.
- Custom tables (and future typed tables without a policy) use `table.<name>.read|create|write|delete`. These keys are
  also accepted as an alternative for mapped tables. Grant them through roles; prefix wildcards such as `table.u_asset_audit.*` or `table.*` work.
- Properties marked `[NotExposed]` (password hashes, secrets) are never part of a table definition and can therefore
  never be read, filtered, sorted, written or rendered into notifications.

## Tables and fields

`PlatformTableRegistry` merges the typed tables discovered by reflection (`[RecordTable]` entities) with the custom
tables and custom fields stored in the database. Definitions are cached and invalidated after every metadata change.

- **Custom tables** (`CustomTable`, technical name `u_*`) store their records in `CustomRecord.DataJson`. An optional
  number prefix gives records numbers like `AUD0001001` (resolvable via `/go/AUD0001001`). The display field is used
  for references, lists and notifications.
- **Custom fields** (`CustomField`, `u_*`) can be added to custom tables and to typed tables that implement
  `IHasCustomFields` (values in `CustomFieldsJson`). Types: text, multi-line text, whole number, decimal, true/false,
  date and time, date, choice (with choice list), reference (to any table), identifier, e-mail, URL, JSON. Options:
  mandatory, default value, help text, maximum length, order. The type and technical name cannot change afterwards.
- **Index for filtering.** Custom field values are mirrored into `RecordFieldValue` rows (normalised text, number and
  date columns) by `RecordFieldIndexer`, so conditions, search and sorting on custom fields run in the database on
  SQLite, SQL Server and PostgreSQL without JSON functions.
- **Layouts** (`TableLayout`): form sections with ordered fields and default list columns, edited on the *Form layout*
  and *List columns* tabs. Without a saved layout a sensible default is generated; fields added later always appear in
  a trailing *New fields* section so they are never hidden.

### Generic list and form

`/table/{name}` lists any table with KPIs, condition filter (`ConditionBuilder`, also accepted as `?query=<condition
JSON>`), free-text search, sortable columns and server-side paging. `/table/{name}/{id}` renders the form from the
layout with type-aware inputs (`FieldInput`), reference pickers, a state flow for `State`/`Status` choice fields, a key
facts side card, SLA timers, activity stream, attachments and an *API view* tab. Only changed fields are sent on save;
validation errors from the service are shown as toasts.

Embedding custom fields into a typed module form:

```razor
<CustomFieldsSection TableName="incident" @bind-Json="_model.CustomFieldsJson" @ref="_custom" />
@code {
    // before saving
    var error = _custom.Validate();
}
```

## Conditions

`ConditionGroup` (AND/OR, nested up to 10 levels, at most 100 rules) is evaluated in memory by `ConditionEvaluator`
and translated to EF Core expressions by `ConditionExpressionBuilder` – both share `ConditionValueConverter` so results
are identical. Operators: equals, not equals, contains, does not contain, starts with, ends with, greater / less
(or equal), is empty, is not empty, in, not in, is me, changes, changes to, changes from.

- Type coercion: enums by numeric value or name, GUIDs, ISO dates and date-times, numbers (invariant), booleans
  (`true/false/yes/no/1/0`), JSON elements.
- Change operators use the previous values of a record event; in database queries they are ignored (never match).
- Only fields of the table definition can be used; values are always parameters (no SQL text is built from input).

## Business rules

Declarative `sys_script` equivalent (`BusinessRule`): table, timing, events (insert / update / delete), order,
condition and an ordered list of actions.

| Action | Before (synchronous, `IRecordSavingHook`) | After (asynchronous, `IRecordEventHandler`) |
|--------|-------------------------------------------|---------------------------------------------|
| Set field value | changes the pending record | updates the committed record |
| Abort with message | rejects the save with the message | – |
| Add work note | added after commit | added |
| Start flow | – | `IFlowExecutionService.StartAsync` with table, record id and number |
| Send notification | – | dispatches the selected notification |

Messages and work notes support `${Field}` placeholders. Compiled rules are cached per table and invalidated on
change. Follow-up updates of after rules raise record events again; the kernel limits cascades to depth 5.

## SLAs

`SlaDefinition`: table, target duration, start / pause / stop / cancel conditions, schedule (24×7 or business hours
with time zone, business days and day start / end – daylight saving changes handled) and an optional breach
notification. `SlaRecordEventHandler` attaches, pauses, resumes, completes and cancels `TaskSla` timers on record
events; `SlaEvaluatorService` (hosted, every `SlaEvaluationIntervalSeconds`, bounded batches) marks breaches, writes
an activity entry on the record and sends the breach notification once. `ISlaService.GetForRecordAsync` feeds
`SlaTimersCard`; `GetBreachedRecordIdsAsync` supports list KPIs with one query. Timers of records the user cannot read
are hidden.

## Notifications

`NotificationDefinition`: table, events, condition (change operators allowed), content (template or inline subject /
body), recipients (user / group reference fields of the record, fixed groups and users, fixed addresses), channels
(e-mail via `IEmailSender`, in-app inbox) and *exclude the actor*. `NotificationTemplate` holds reusable plain-text
subject and body with `${Field}` placeholders plus `${table}`, `${link}`, `${actor}`, `${event}`.

Rendering (`NotificationRenderer` / `TemplateRenderer`) prefers display values, HTML-encodes every value, strips line
breaks from subjects and wraps the body in a branded HTML layout (`EmailLayout`) with a link to the record
(the "Public URL" system setting `general.base_url` – System settings › General – makes links absolute; `Servora:Platform:PublicUrl` is the fallback when the setting is empty). Only exposed fields can be used. The studio offers a live
preview against the most recent record. A default template *Record notification* is seeded.

**In-app inbox.** `INotificationInbox` exposes the current user's messages only. The `NotificationBell` component
(unread badge, latest messages, polling every 60 s) is meant for the top bar; `/notifications` is the full inbox.

## Table REST API

All endpoints require authentication (cookie or API key) plus `api.access`; table permissions are enforced by
`IRecordService`. Errors are RFC 7807 problem details (400 validation, 403, 404, 409, 413, 415, 422).

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/table/{table}` | Query records. |
| GET | `/api/v1/table/{table}/{id}` | Get a record. |
| POST | `/api/v1/table/{table}` | Create a record from a JSON object (201 + `Location`). |
| PATCH | `/api/v1/table/{table}/{id}` | Update the given fields. |
| DELETE | `/api/v1/table/{table}/{id}` | Delete a record (204). |
| GET | `/api/v1/go/{number}` | Resolve a record number to table, id and UI route. |

Query parameters of the list endpoint:

| Parameter | Meaning |
|-----------|---------|
| `sysparm_query` | Condition JSON, e.g. `{"logic":"And","rules":[{"field":"Priority","operator":"LessOrEqual","value":"2"}]}` (max 16 000 chars). |
| `sysparm_fields` | Comma separated field list (default: all exposed fields). |
| `sysparm_limit` | Page size, 1–500 (default 25). |
| `sysparm_page` / `sysparm_offset` | Page number (1-based) or row offset. |
| `sysparm_order_by`, `sysparm_order_desc` | Sort field and direction. |
| `sysparm_search` | Free-text search over text fields (max 200 chars). |
| `sysparm_display_value` | `false` omits the `Field.display` values of references and choices. |

The response is `{ "result": [...], "total": n, "page": p, "pageSize": s }` with an `X-Total-Count` header. Write
bodies must be `application/json` objects (max 256 KB, depth 16); unknown and read-only fields are rejected.

```http
POST /api/v1/table/u_asset_audit
Authorization: <API key header as documented by the API module (T-10)>
Content-Type: application/json

{ "u_title": "Laptop audit", "u_state": "new", "u_auditor": "5f0b6a3e-1f43-4a5e-9d4c-2c1f0e7d9a10" }
```

**Inbox API** (`/api/v1/notifications`, authenticated user): `GET /?unread=true&page=1&pageSize=20`,
`GET /unread-count`, `POST /{id}/read`, `POST /read-all`, `DELETE /{id}`.

## Configuration (`appsettings.json`, section `Servora:Platform`)

| Key | Default | Meaning |
|-----|---------|---------|
| `PublicUrl` | – | Fallback base URL for links in e-mails (`https://servora.example.com`); the "Public URL" system setting (`general.base_url`) takes precedence. |
| `SlaEvaluationIntervalSeconds` | 30 | Breach evaluation interval (minimum 5). |
| `SlaBatchSize` | 200 | Timers per evaluation batch (at most 10 batches per run). |

## Security notes

- No code execution: rules, SLAs and notifications are declarative; placeholders are bounded look-ups.
- Every service method checks permissions server-side; pages additionally carry `[RequirePermission]`.
- All queries are bounded (paging ≤ 500, lists ≤ 1000, condition size/depth limits, body size limits) and
  provider-neutral (no raw SQL, no ordering on decimal columns in the database).
- Configuration changes (tables, fields, layouts, rules, SLAs, notifications, templates) are audited.
- E-mail preview is rendered in a sandboxed `iframe`; all record values are HTML-encoded; URL fields only render
  `http(s)` links.
