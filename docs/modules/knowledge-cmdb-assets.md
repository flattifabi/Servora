# Knowledge, CMDB & Assets (T-06)

Three closely related modules: **Knowledge Management** captures and publishes solutions, the **CMDB** describes the
services and infrastructure and how they depend on each other, and **Asset Management** tracks the financial and physical
lifecycle of hardware, software and licenses.

## Tables

| Table | Entity | Number | Notes |
|-------|--------|--------|-------|
| `kb_knowledge` | `KnowledgeArticle` | `KB0001001` | Title, summary, Markdown body, keywords, knowledge base, category, state, author, reviewer comment, published/retired at, valid to, view count, helpful / not helpful counts, version number. |
| `kb_knowledge_base` | `KnowledgeBase` | – | Name, description, icon, active flag, order. |
| `kb_category` | `KnowledgeCategory` | – | Category per knowledge base. |
| `kb_version` | `KnowledgeArticleVersion` | – | Snapshot (title, summary, body, change note) written on creation and on every content change. |
| `kb_feedback` | `KnowledgeFeedback` | – | One helpful / not helpful vote per user and article (changing a vote updates the counters). |
| `kb_m2m_ci` | `KnowledgeArticleCi` | – | Article ↔ configuration item links ("related CIs"). |
| `cmdb_ci` | `ConfigurationItem` (kernel) | – | Class-specific attribute values are stored in `CustomFieldsJson` (see below). |
| `cmdb_class` | `CiClass` | – | Key, label, icon, color, parent class, order, system flag, attribute definitions as JSON. |
| `cmdb_rel_ci` | `CiRelationship` | – | Parent → child with type *Depends on*, *Runs on*, *Hosted on*, *Connects to*, *Contains* and an optional note. Unique per (parent, child, type). |
| `alm_asset` | `Asset` | `AST0001001` | Category (hardware / software / license), model, manufacturer, vendor, serial, asset tag (unique), state, assigned to, location, cost + currency, purchase date, warranty end / license expiry, linked CI, entitlements / installs, notes, retired at. |

All record tables carry `[RecordTable]` and `[Reference]` attributes, so they appear in the table registry, business
rules, flows, reports and the Table API. EF mappings live in `Infrastructure/Persistence/Configurations/{Knowledge,Cmdb,Assets}`
(max lengths on every string, indexes on lookups, unique indexes on numbers, keys and asset tags).

## Knowledge Management

### Workflow

```
Draft ──► Review ──► Published ──► Retired
  ▲   └──────────────►┘   │           │
  └── Reject ◄── Review    └─ Revise ──┴──► Draft (new revision)
```

`KnowledgeWorkflow` (domain) holds the pure rules. *Submit for review* and editing need `knowledge.write`; *Publish*,
*Reject* and *Retire* need `knowledge.publish` (reviewer). Only drafts and articles in review are editable; a published
article is changed by creating a new revision (*Revise*). Creating an article and every change of title, summary or
body writes a `kb_version` snapshot and increments the article version; old versions can be viewed in the reader. Readers with only `knowledge.read` see published
articles whose *valid to* date has not passed; drafts are visible to authors and reviewers only. Workflow actions are
journaled to the activity stream and publishing / retiring is audited.

### Safe Markdown

`SafeMarkdown` is an in-house renderer (no packages) with a strict security model:

* Raw HTML is never passed through – every source character is HTML-encoded unless it is recognised Markdown syntax.
* Only a fixed allow-list of tags and attributes is emitted.
* Links are limited to `http`, `https`, `mailto`, site-relative paths and fragments (`javascript:`, `data:`,
  `vbscript:`, protocol-relative and obfuscated variants are rejected); external links get `rel="noopener noreferrer nofollow"`.
* Images are rendered as links, never embedded (no tracking pixels / data URIs).
* Heading ids are prefixed with `kb-` (no DOM clobbering); input length and nesting depth are bounded.

Supported subset: ATX headings, paragraphs, hard line breaks, bold, italic, strikethrough, inline code, fenced code
blocks, block quotes, nested lists, links, autolinks, horizontal rules and GitHub-style tables. The headings feed the
reader's table of contents. `SafeMarkdownSecurityTests` covers the XSS vectors.

### Search – `IKnowledgeSearchService`

Provider-neutral candidate retrieval (case-insensitive `Contains` per query term on title, summary, keywords and body,
bounded) followed by in-memory ranking with `KnowledgeRanker`: weighted term frequency (title > keywords > summary >
body), phrase bonus, term coverage and a small popularity boost (views, helpful votes). Returns snippets around the first
match. Used by the knowledge home, the portal (T-11) and AI / MCP tools (T-10).

### Pages

| Route | Purpose |
|-------|---------|
| `/knowledge` | Search-first hero with live results and highlighting, knowledge base / category tiles, popular and new articles. |
| `/knowledge/articles` | Article management list (state filter, KPIs, my drafts, awaiting review). |
| `/knowledge/{id}` | Reader view: state flow, rendered article, sticky table of contents, helpful votes, related CIs, versions, activity, attachments. Views are counted. |
| `/knowledge/new`, `/knowledge/edit/{id}` | Editor with toolbar and live preview (same safe renderer), metadata side panel. |
| `/knowledge/admin` | Knowledge bases and categories (`knowledge.manage`). |

## CMDB

### Classes and attributes

CI classes form a hierarchy (`virtual_machine` → `server`); sub-classes inherit all parent attributes. Each class stores
its attribute definitions (name, label, type *Text / Number / Boolean / Date / Choice*, required, choices, unit,
help) as JSON. `CiAttributeSchema` validates definitions (max 50 per class, unique names, inherited names cannot be
redefined) and values, and merges values into `ConfigurationItem.CustomFieldsJson`. Keys that are not attributes of the
class are preserved, so platform custom fields (T-07) can share the same JSON object. On the CI form the attributes are
rendered as typed fields (`CiAttributeFields`). Built-in classes cannot be deleted; a class with CIs or sub-classes
cannot be deleted either.

### Relationships and the dependency graph

Relationships are directed (parent → child, read "parent *depends on* child"). *Downstream* = what a CI needs; *upstream* =
what is affected when it fails (impact analysis). `ICmdbService.GetGraphAsync` loads the neighbourhood level by level
(bounded edges per level) and `CiGraphBuilder` performs a breadth-first traversal with cycle protection, a maximum depth
of 6 and a node limit of 120 (`Truncated` flag). Nodes carry a signed level (negative = upstream). `CiGraphLayout`
computes a **layered** (left-to-right) or **radial** layout; `CiDependencyGraph` renders it as interactive SVG
(direction and depth controls, hover highlighting of connected edges, status rings, legend, zoom, click to navigate).

### Related records

The *Related records* tab lists incidents, problems, changes and requests that reference the CI through
`IRecordService` (only tables the user can read; unavailable modules are listed instead of failing), plus knowledge
articles and assets linked to the CI.

### Pages

| Route | Purpose |
|-------|---------|
| `/cmdb` | KPIs by operational status, class filter chips (including sub-classes), environment / status filters, table. |
| `/cmdb/new`, `/cmdb/{id}` | Record page: general / ownership / technical / class attribute sections, key facts, tabs Dependency map, Relationships (editor), Related records, Activity, Attachments. |
| `/cmdb/map` | Full-width service map starting from a business service. |
| `/cmdb/classes` | Class administration with attribute editor (read-only without `cmdb.manage`). |

## Asset Management

### Lifecycle

```
In stock ⇄ In use ⇄ In repair
   │          │          │
   └──────────┴──────────┴──► Retired ──► Disposed (final, read-only)
                               └──► In stock (reactivate)
```

`AssetLifecycle` (domain) holds the transitions. New assets start *in stock* or *in use*. Hardware must be assigned to a
user before it is put in use; returning to stock clears the assignment; retiring sets `RetiredAt`. Transitions accept an
optional note (activity stream); retire / dispose / delete are audited. Updates use optimistic concurrency (`Version`).

### KPIs – `IAssetService.GetOverviewAsync`

Totals per state and category, purchase value of active assets per currency, warranties expiring within 90 days and
already expired, license entitlements vs. installs and the number of over-deployed licenses, purchases per month for the
last 12 months. Decimal aggregates are computed in memory over a bounded projection (SQLite cannot sum / order decimals).
`AssetDisplay` contains the presentation rules (warranty classification and text, license utilisation, money formatting).

### Pages

| Route | Purpose |
|-------|---------|
| `/assets` | KPI cards (value, in use, expiring warranties, license seats), state donut, 12-month purchase chart, category chips, warranty-attention filter, table with warranty pills and seat meters. Query `?filter=warranty|active|license`. |
| `/assets/new`, `/assets/{id}` | Record page: state flow, lifecycle action buttons with note dialog, general / assignment / financial / license compliance sections, side cards (purchase value, warranty progress, seats, key facts, linked CI), Activity and Attachments tabs. `?category=license` preselects the category. |

## REST endpoints

Read-only adapters (`KnowledgeCmdbEndpoints`); the services enforce the permissions.

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/api/knowledge/search?q=&take=` | `knowledge.read` |
| GET | `/api/knowledge/articles/{id}` | `knowledge.read` (drafts only for authors / reviewers) |
| GET | `/api/cmdb/cis/{id}/graph?depth=&direction=Both|Upstream|Downstream` | `cmdb.read` |

## Permissions

| Permission | Grants |
|-----------|--------|
| `knowledge.read` | Read published articles, search, vote. |
| `knowledge.write` | Author, edit drafts, submit for review, revise, link CIs. |
| `knowledge.publish` | Publish, reject, retire. |
| `knowledge.manage` | Knowledge bases and categories, delete articles. |
| `cmdb.read` / `cmdb.write` / `cmdb.manage` | View; create / update CIs and relationships; manage classes and delete CIs. |
| `asset.read` / `asset.write` / `asset.manage` | View; create / update / lifecycle; delete. |

Every application service method checks its permission server-side and returns `Result.Forbidden()` otherwise; pages
carry `[RequirePermission]` and hide actions the user cannot perform.

## Seed data

Idempotent seeders (only when the respective table is empty): `CmdbSeeder` (order 110) – 7 classes (Business Service,
Application, Server, Virtual Machine ⊂ Server, Database, Network Device, Storage) with attributes and ~30 CIs forming a
service map (Online Shop, Corporate Email, HR Portal, … down to hosts, SAN and network); `AssetSeeder` (120) – 21 assets
(laptops, servers, network gear, software, licenses incl. an over-deployed one and expiring warranties) linked to users
and CIs; `KnowledgeSeeder` (130) – 2 knowledge bases, 8 categories and 8 articles (6 published, 1 in review, 1 draft) with related CIs.

## Tests

`tests/Servora.Tests.Unit/KnowledgeCmdb`: safe Markdown rendering and XSS vectors, knowledge ranking, KB workflow service
(permissions, versions, votes, visibility), graph traversal / layout, CI attribute schema, CMDB relationships, asset
lifecycle, overview KPIs, seeders' idempotency and list filters, asset display rules.
