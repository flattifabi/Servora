# Self-Service Portal (T-11)

The portal is the consumer-grade front door of Servora for employees: search for answers, order from the service
catalog, report issues, follow their own requests and incidents, talk to the support team and decide approvals. It has
its own layout (`PortalLayout`) and lives under `/portal`. A user holding **only `portal.access`** can use every portal
feature; all data access is restricted server-side to the user's own records or to published knowledge.

## Pages

| Route | Page |
|-------|------|
| `/portal` | Home: greeting hero with live search (knowledge, catalog, own items), quick actions, announcements, KPI tiles (open requests, open issues, awaiting confirmation, approvals), my open items, popular answers, popular requests (most ordered in the last 90 days). |
| `/portal/search?q=` | Full search results in three sections. |
| `/portal/catalog` (`?category=`) | Catalog browsing with search and category chips. |
| `/portal/catalog/{id}` | Order form rendering the item's variables (`CatalogVariableInput`), inline validation, quantity, summary, "what happens next", confirmation with "Track my request". |
| `/portal/report` | "Report an issue": summary with live knowledge suggestions (deflection), details, category, urgency cards, affected-people chips, up to 5 files (uploaded right after the incident is created). |
| `/portal/my` | My items: incidents and requests (open / closed / all, kind chips, search, "show more"). |
| `/portal/incidents/{id}` | Issue view: status timeline, "Is everything working again?" (accept → Closed / reopen with reason → In Progress), description, solution, conversation, details, who is helping. |
| `/portal/requests/{id}` | Request view: status timeline (Submitted → Approval → Fulfilment → Delivered), requested items with answers, approval label and delivery progress, conversation, details, cancel with reason. |
| `/portal/knowledge` (`?category=`, `?q=`) | Knowledge search, topic tiles, most read, recently published, category article lists. |
| `/portal/knowledge/{id}` | Reader: safe rendered Markdown (`MarkdownView`), table of contents, helpful votes, related articles, hand-off to "Report an issue". Views are counted. |
| `/portal/approvals` | Approvals for approvers: waiting / history, 30-day stats, expandable details (requested item answers, change window and risk), approve (optional comment) / reject (reason required). |
| `/portal/admin/announcements` | Announcement administration in the application shell (`portal.manage`): KPIs, table, drawer editor with live preview, delete confirmation. |

All portal pages carry `[RequirePermission(Permissions.Portal.Access)]` (the admin page `portal.manage`). The layout
hides the splash, enforces the forced password change like `MainLayout`, shows an approvals badge, a "Report an issue"
call to action, theme switch, user menu ("Open workspace" only for users with fulfiller permissions) and collapses the
navigation into a drawer below 1080 px. Pages are verified down to 375 px without horizontal scrolling.

## Application services (`Servora.Application.Portal`)

| Contract | Highlights |
|----------|-----------|
| `IPortalTicketService` | My items list (incidents + requests merged, bounded window ≤ 1000), incident detail, request detail (delegates to `IRequestService.GetAsync` after the ownership check), issue categories, **report issue** (incident with caller = opened by = current user, channel *Portal*, priority from `PriorityMatrix`), accept resolution, reopen with reason (posted as a public comment), cancel request (delegates to `IRequestService.CancelRequestAsync`). |
| `IPortalConversationService` | Public conversation of an own item: comments and system events only (work notes and field changes are never read), request conversations also show the public events of the request's items; add comment (always `ActivityType.Comment`), upload attachment, open attachment (ownership of the attachment's record is checked). Delegates to `IActivityService` / `IAttachmentService`. |
| `IPortalKnowledgeService` | Published-only knowledge: home, ranked search (reuses `KnowledgeRanker` + `SafeMarkdown`), category lists, reader, view counter, one vote per user. Accepts `portal.access` or `knowledge.read`. |
| `IPortalApprovalService` | What an approver approves (requested item facts and answers, change facts). Only for the approval's approver (or `approval.read`). Deciding uses the approvals module's `IApprovalInboxService`. |
| `IPortalAnnouncementService` | Live announcements (`portal.access`); list / save (optimistic concurrency) / delete (`portal.manage`, audited as `portal.announcement.*`). |
| `IPortalHomeService` | Home data (counts restricted to own records, popular catalog items via `ICatalogOrderService`, approvals count via `IApprovalService.CountPendingForUserAsync`) and the hero search. |

Pure logic: `PortalStatusMapper` (consumer statuses and timelines), `PortalLinkPolicy` (announcement links: site-relative
paths or absolute `https://` only), `PortalIssueInput` / `PortalAnnouncementInput` validation.

## Security model

* **Who**: every method requires a *user* actor holding `portal.access` (`PortalScope.PortalUserAsync`); otherwise
  `Result.Forbidden()`.
* **What**: every query starts from `PortalScope.OwnIncidents` (`CallerId == me || OpenedById == me`) or
  `PortalScope.OwnRequests` (`RequestedForId == me || OpenedById == me`). Records of other users are reported as
  **not found** (no existence oracle) – for reading, commenting, uploading, downloading, accepting, reopening and
  cancelling. This also applies to users with broader permissions: the portal only ever shows their own items.
* **Comments only**: portal users can never read or write work notes; closed items accept no new comments or files.
* **Attachments** are downloaded through `GET /portal/attachments/{id}`, which checks ownership of the attachment's
  record and always serves `application/octet-stream` (no inline rendering).
* **Knowledge**: only `Published` articles of active knowledge bases whose *valid to* date has not passed – drafts,
  reviews, retired and expired articles are invisible regardless of other permissions. Article HTML comes exclusively
  from `SafeMarkdown`.
* **Approvals**: the inbox only returns the user's own approvals; details are only shown to the approver.
* **Announcements**: plain text (HTML-encoded by Razor), links validated on save and again on render.
* All queries are bounded and provider neutral (no raw SQL, ordering on timestamps only, decimal prices never ordered in SQL).

## Data model

| Table | Entity | Notes |
|-------|--------|-------|
| `portal_announcement` | `PortalAnnouncement` | Title (160), body (2000), kind (Info / Maintenance / Outage / Success), optional link + text, display window (starts / ends), active flag, order. Index on (active, starts). Outages are always listed first. |

`PortalSeeder` (order 600) adds one welcome announcement once (marker `portal.demo_data.seeded`). No migrations are part
of this task (T-13).

## Permissions

| Permission | Meaning |
|------------|---------|
| `portal.access` | Use the portal (built-in roles `user` and `itil`). |
| `portal.manage` | Manage portal announcements (new; only `admin` holds it by default). |

## Tests

`tests/Servora.Tests.Unit/Portal` (in-memory SQLite with the real ITSM, service management, knowledge and portal DI
graphs and saving hooks):

* `PortalIncidentTests` – report as caller (channel, priority), other user cannot list / read / comment / upload /
  download / accept / reopen / find by number, conversation without work notes, attachments, accept and reopen flows,
  everything forbidden without `portal.access`.
* `PortalRequestTests` – requester sees answers and timeline, other user sees nothing and cannot cancel or comment,
  requested-for visibility, approval timeline, approver details only for the approver, home counts and popularity.
* `PortalKnowledgeTests` – only published / valid / active-base articles, portal-only user can read, votes and views.
* `PortalAnnouncementTests` – display window and ordering, `portal.manage` required, unsafe links rejected, concurrency,
  audit.
* `PortalLogicTests` – link policy, status mapping, timelines, input validation.

## Known limitations

* Portal-only users read knowledge through `IPortalKnowledgeService`, which repeats the candidate retrieval and vote
  logic of the knowledge module (it reuses ranker and renderer) because `IKnowledgeService` / `IKnowledgeSearchService`
  require `knowledge.read`.
* The kernel `IAttachmentService` and `/attachments/{id}` have no record-level authorization; the portal therefore uses its
  own ownership-checked download endpoint.
* Users cannot order on behalf of others in the portal (fulfillers use `/requests/new`).
* The greeting uses the server's local time.
