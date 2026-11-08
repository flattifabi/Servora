# Self-Service Portal (T-11)

The portal is the consumer-grade front door of Servora for employees: search for answers, order from the service
catalog, report issues, follow their own requests and incidents, talk to the support team and decide approvals. It has
its own layout (`PortalLayout`) and lives under `/portal`. A user holding **only `portal.access`** can use every portal
feature; all data access is restricted server-side to the user's own records or to published knowledge.

## Pages

| Route | Page |
|-------|------|
| `/portal` | Home (P-1, reference design 1): mint hero with configurable greeting (`{name}` = first name), headline, subline, live search with a *Search* button and "Frequently searched" terms, hero image card (uploaded image or built-in brand-coloured SVG illustration) with caption chip; live announcements; "What do you need today?" category cards (icon, description, real number of services, "All N services"); "Frequently used. Quickly found." item cards (featured items in their order, otherwise the most ordered; "Most ordered" badge for the item ordered most in 90 days, item badge otherwise, favourite heart, delivery line, "from €39.00 / month" or "No additional cost", Configure / Request); "My requests" table (latest 4 items of any state, open count); "Questions?" support card. |
| `/portal/search?q=` | Full search results in three sections. |
| `/portal/catalog` (`?category=`) | Catalog browsing with search, category chips, a favourites filter and item cards with favourite hearts. |
| `/portal/catalog/{id}` | Item page (P-1, reference design 2): breadcrumb, image or icon panel with badge, category eyebrow, title, subtitle, description, highlight facts, *Save* (favourite); tabs *Configuration* / *Details & equipment* / *Delivery & support* (safe Markdown) with the service ID; numbered section cards built from the item's sections (first card: *Requested for* – own name, "Order for someone else" with a user picker for `request.write`; variables without a section go to a general card); `PortalOrderField` renders priced choices as radio cards with "+ €8.00 / month", other choices as selects, priced yes / no questions as accessory cards, confirmations as check boxes, text areas with a character counter; the cost centre is pre-selected from the requester's department when an option matches. Sticky summary: chosen options, price lines, one-time costs, "Monthly billing" box with cost centre and term, approval note naming the approver, *Request now*, fine print; "What happens next" steps and "Contact IT advice" (support link). Required fields are validated inline; the price is recomputed by the catalog service when ordering. |
| `/portal/report` | "Report an issue": summary with live knowledge suggestions (deflection), details, category, urgency cards, affected-people chips, up to 5 files (uploaded right after the incident is created). |
| `/portal/my` | My requests: incidents and requests (open / closed / all, kind chips, search, "show more"); requests show their price ("€52.00 / month"). |
| `/portal/incidents/{id}` | Issue view: status timeline, "Is everything working again?" (accept → Closed / reopen with reason → In Progress), description, solution, conversation, details, who is helping. |
| `/portal/requests/{id}` | Request view: status timeline (Submitted → Approval → Fulfilment → Delivered), requested items with answers, approval label and delivery progress, conversation, details, cancel with reason. |
| `/portal/knowledge` (`?category=`, `?q=`) | Knowledge search, topic tiles, most read, recently published, category article lists. |
| `/portal/knowledge/{id}` | Reader: safe rendered Markdown (`MarkdownView`), table of contents, helpful votes, related articles, hand-off to "Report an issue". Views are counted. |
| `/portal/approvals` | Approvals for approvers: waiting / history, 30-day stats, expandable details (requested item answers, change window and risk), approve (optional comment) / reject (reason required). |
| `/portal/admin/announcements` | Announcement administration in the application shell (`portal.manage`): KPIs, table, drawer editor with live preview, delete confirmation. |
| `/portal/admin/settings` | Portal settings in the application shell (`portal.manage`, P-1): hero texts, frequently searched terms, hero image upload / removal, support card, footer tagline and links, currency; live hero preview. |

All portal pages carry `[RequirePermission(Permissions.Portal.Access)]` (the admin pages `portal.manage`). The layout
(P-1) has a brand-ink top bar: logo tile (uploaded branding logo or a default mark) + product name, "Service portal",
tabs *Service catalog* (also active on the home page and search), *My requests* with the number of open items,
*Knowledge base* and – while approvals wait or on the approvals page – *Approvals* with a badge; a bell with pending
approvals and live announcements; the user's initials, name and menu (my requests, report an issue, approvals, profile,
password, theme, portal settings / announcements for `portal.manage`, open workspace for fulfillers, sign out). The
footer shows the product name, the configurable tagline, a live status ("All systems operational" / "Maintenance in
progress" / "Service disruption" derived from live announcements) and the configurable Contact / Privacy /
Accessibility links (hidden when empty). It hides the splash, enforces the forced password change like `MainLayout` and
collapses the navigation into a drawer below 1080 px. Pages are verified down to 375 px without horizontal scrolling.

**Design (P-1):** every colour is derived from the design tokens (`--primary`, `--brand-ink`, `--surface-tint`,
`--border`, ...) with fallbacks matching the reference design and `color-mix(...)` tints of `--primary`, so the portal
follows the brand colour configured in the admin centre. The layout defines portal aliases (`--p-primary`, `--p-ink`,
`--p-tint`, ...) and the shared vocabulary (`.portal-container`, `.pcard`, `.pbtn`, `.ppill`, `.picon-tile`,
`.psection-head`, ...) with `::deep`; components: `PortalCategoryCard`, `PortalSpotlightCard`, `PortalRequestTable`,
`PortalSupportCard`, `PortalHeroIllustration`, `PortalOrderField`, `PortalGlyph` (a few extra icons such as heart and
headset).

## Portal settings

`IPortalSettingsService` (`Servora.Application.Portal`, implemented in `Infrastructure/Portal/PortalSettingsService`)
stores the configurable texts as system settings (`PortalSettingDefinitionProvider`, keys `portal.*`, category
*Portal*): greeting, headline, subline, search placeholder, frequently searched terms (max. 6), image caption, support
eyebrow / title / text / button text / link / hours, footer tagline, contact / privacy / accessibility links and the
currency (EUR, USD, GBP, CHF). Reading requires `portal.access` or `portal.manage`; saving requires `portal.manage`.
Because `ISystemSettingsService` writes require `admin.settings.manage`, the service writes the (validated) `portal.*`
keys in a child scope whose actor is the current user restricted to exactly that permission - so the settings audit
trail names the real user - and writes its own audit entry `portal.settings.updated`. `PortalSettingsValidator` checks
lengths and links: `PortalUrlPolicy` allows site-relative paths and absolute `http(s)` URLs only (no scriptable schemes,
protocol-relative URLs, back-slashes, white space or user info); links are re-checked before rendering. All texts are
plain text and HTML-encoded by Razor. The hero image is verified by magic bytes (PNG, JPEG, GIF, WebP; max. 2 MB),
stored as `<data dir>/portal/hero.bin` and served by `GET /portal/hero-image` (`nosniff`, cache buster in the URL).

**Landing page (T-13):** users who hold `portal.access` but none of `incident.read`, `problem.read`, `change.read`,
`request.read` (for example the built-in `user` role) are redirected from `/` (My Work) to `/portal`
(`PortalLandingPolicy`). Fulfillers and administrators stay on My Work.

## Application services (`Servora.Application.Portal`)

| Contract | Highlights |
|----------|-----------|
| `IPortalTicketService` | My items list (incidents + requests merged, bounded window ≤ 1000), incident detail, request detail (delegates to `IRequestService.GetAsync` after the ownership check), issue categories, **report issue** (incident with caller = opened by = current user, channel *Portal*, priority from `PriorityMatrix`), accept resolution, reopen with reason (posted as a public comment), cancel request (delegates to `IRequestService.CancelRequestAsync`). |
| `IPortalConversationService` | Public conversation of an own item: comments and system events only (work notes and field changes are never read), request conversations also show the public events of the request's items; add comment (always `ActivityType.Comment`), upload attachment, open attachment (ownership of the attachment's record is checked). Delegates to `IActivityService` / `IAttachmentService`. |
| `IPortalKnowledgeService` | Published-only knowledge: home, ranked search (reuses `KnowledgeRanker` + `SafeMarkdown`), category lists, reader, view counter, one vote per user. Accepts `portal.access` or `knowledge.read`. |
| `IPortalApprovalService` | What an approver approves (requested item facts and answers, change facts). Only for the approval's approver (or `approval.read`). Deciding uses the approvals module's `IApprovalInboxService`. |
| `IPortalAnnouncementService` | Live announcements (`portal.access`); list / save (optimistic concurrency) / delete (`portal.manage`, audited as `portal.announcement.*`). |
| `IPortalHomeService` | Home data (counts restricted to own records, popular catalog items via `ICatalogOrderService`, approvals count via `IApprovalService.CountPendingForUserAsync`; P-1: categories with real counts, total services, spotlight items, most ordered item, latest items, favourites), open item count for the navigation badge and the hero search. Request summaries carry their price and billing period. |
| `IPortalSettingsService` | Portal texts, links, currency and hero image (see *Portal settings*). |

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

`PortalSeeder` (order 600) adds one welcome announcement once (marker `portal.demo_data.seeded`). Portal settings live
in `sys_properties` (no own table). The catalog model extensions used by the portal (pricing, presentation, sections,
favourites) come with the migration `AddCatalogPortalExperience` for SQLite, SQL Server and PostgreSQL (see
[service-management.md](service-management.md)).

## Permissions

| Permission | Meaning |
|------------|---------|
| `portal.access` | Use the portal (built-in roles `user` and `itil`). |
| `portal.manage` | Manage portal announcements and the portal settings (texts, links, hero image; only `admin` holds it by default). |

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
* `PortalSettingsTests` (P-1) – URL policy, settings validation, defaults, `portal.manage` is enough to save (narrowly
  elevated settings write, audited), everything forbidden without portal permissions, hero image type check / storage /
  removal (with `PortalActorAwareUser`, a test actor that honours `ActorContext` like the production `CurrentUser`).

## Known limitations

* Portal-only users read knowledge through `IPortalKnowledgeService`, which repeats the candidate retrieval and vote
  logic of the knowledge module (it reuses ranker and renderer) because `IKnowledgeService` / `IKnowledgeSearchService`
  require `knowledge.read`.
* The kernel `IAttachmentService` and `/attachments/{id}` have no record-level authorization; the portal therefore uses its
  own ownership-checked download endpoint.
* Ordering on behalf of others in the portal is offered only to users holding `request.write` (enforced by the catalog
  service).
* "Save as draft" from the reference design is not offered: requests have no draft state.
* Without an uploaded product image, item cards show an icon panel with the teaser instead of a photo (no stock photos
  are shipped).
* The greeting uses the server's local time.
