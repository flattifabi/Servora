# UI Guidelines & Component Catalog

Servora must look and feel like premium enterprise software: **light, calm and premium**. White cards with 1px soft
grey-green borders and 10–12px radius, very light mint tints for heroes and selected states, a deep brand ink for the
sidebar / portal header, dark headings with tight letter spacing, muted grey body text, small uppercase eyebrow labels
and almost no shadows. No gradients, no glows. Use these building blocks – never hand-roll colors, spacing or ad-hoc
widgets. All styles live in `src/Servora.Web/wwwroot/css/servora.css`; module-specific styling goes into scoped
`*.razor.css` files and must only use the tokens below.

## Theming & branding

- **Light is the default theme.** Dark is opt-in through `[data-theme="dark"]` on `<html>` (top bar toggle or the
  profile preference). Stored preference values are `light`, `dark` and `system` (follows the OS); a missing value
  means light. `App.razor` applies the preference before the first paint, `servora.js` (`setTheme`, `getTheme`,
  `getAppliedTheme`) changes it at runtime, and the profile theme is handed over after sign-in (`UserThemeCookie`).
- **One brand colour drives the whole palette.** The accent from *Administration › System Settings › Branding*
  (`branding.accent_color`, default forest green `#1b4535`) is injected by `BrandingState.ThemeStyle` as
  `:root { --brand; --primary; --primary-contrast }`. Every tint and shade (`--primary-soft`, `--primary-strong`,
  `--primary-text`, `--brand-ink`, `--surface-tint`, the focus ring, even the neutral greys) is derived in CSS with
  `color-mix()` and, where supported, relative colours (`oklch(from var(--brand) …)`) that clamp lightness so any
  accent keeps AA contrast. `--primary-contrast` (white or dark ink) is computed server-side from the WCAG luminance.
  Saving the branding recolours the open page immediately (`servora.applyBrand`). The retired default `#7c6cff` is
  treated as "not customised", so existing instances move to the new default.
- Never use raw hex colours for UI chrome in components. Data colours (chart series, CI classes) may be literal, but
  prefer `var(--primary)`, the status tokens or `ChartPalette`.
- To preview another accent locally (branding panel, setup wizard) put `class="brand-scope"` on a container and set
  `--brand`, `--primary` and `--primary-contrast` inline – the derived brand tokens are recomputed for that subtree.

### Token contract

Consumers (agent workspace, portal, sign-in, setup) rely on these names. Keep them stable.

| Token | Meaning |
|-------|---------|
| `--brand` | The configured brand colour as injected (identical in both themes). |
| `--primary` | Interactive brand colour: primary buttons, active tabs, selected borders, checkbox/radio accents. Lighter in the dark theme. |
| `--primary-strong` | Hover/pressed state of `--primary`. |
| `--primary-soft` | Mint tint for icon tiles, active chips, soft badges. |
| `--primary-contrast` | Text/icon colour on `--primary` backgrounds. |
| `--primary-text` | Brand colour for text and links on light surfaces (AA-safe for any accent). |
| `--brand-ink` | Deep shade for the sidebar, portal header and brand marks (text on top: `--brand-ink-contrast`). |
| `--bg` | Page background behind cards. |
| `--surface` | Cards, inputs, menus, modals (white in the light theme). |
| `--surface-2` | Subtle fills: table headers, disabled inputs, modal footers. |
| `--surface-3` | Slightly stronger fill: skeletons, code, tracks. |
| `--surface-hover` | Row / item hover. |
| `--surface-tint` | Very light mint for hero bands, section backgrounds and selected cards. |
| `--border` | Default 1px border. |
| `--border-strong` | Input and secondary button borders. |
| `--text` | Headings and primary text (near-black). |
| `--text-2` | Body text. |
| `--text-muted` | Secondary text, subtitles. |
| `--text-faint` | Captions, table headers, placeholders (still ≥ 4.5:1 on white). |
| `--success`, `--warning`, `--danger`, `--info`, `--purple` | Status colours, readable as text on white and on their soft variant. |
| `--success-soft`, `--warning-soft`, `--danger-soft`, `--info-soft`, `--purple-soft`, `--neutral-soft` | Pill / alert backgrounds. |
| `--r-xs` 6px, `--r-sm` 8px, `--r` 10px, `--r-lg` 12px, `--r-xl` 16px, `--r-full` | Radii (inputs/buttons `--r-sm`, cards `--r-lg`). |
| `--shadow-sm`, `--shadow`, `--shadow-lg` | Shadows – `--shadow-sm` is nearly invisible; `--shadow-lg` only for overlays. |
| `--ring`, `--focus` | Focus ring (`box-shadow`) and focus outline colour. |
| `--font-sans`, `--font-display`, `--font-mono` | System font stacks (Segoe UI Variable first; no web fonts). |

Legacy aliases kept for older styles (do not use in new code): `--accent` / `--accent-soft` (= info), `--gradient`
(flat primary), `--gradient-soft` (flat primary-soft), `--glow` (transparent), `--sidebar` (= brand ink).

Status pill tones follow the reference designs: **blue** in progress, **amber** awaiting / on hold, **green**
completed, **grey** new / draft, **red** failed / cancelled, **violet** problem analysis.

## Page anatomy

**List page**
```razor
@page "/incidents"
@attribute [RequirePermission(Permissions.Incidents.Read)]

<PageTitle>Incidents · Servora</PageTitle>
<PageHeader Title="Incidents" Subtitle="Restore normal service operation as quickly as possible." Icon="alert-triangle">
    <Actions>
        <button class="btn btn-secondary"><Icon Name="download" Size="16" /> Export</button>
        <a class="btn btn-primary" href="/incidents/new"><Icon Name="plus" Size="16" /> New incident</a>
    </Actions>
</PageHeader>

<div class="grid grid-4 mb-6">
    <StatCard Label="Open" Value="128" Icon="inbox" Trend="_trend" />
    <StatCard Label="Critical" Value="3" Icon="flame" Tone="danger" />
    ...
</div>

<DataTable TItem="IncidentRow" Items="_page.Items" TotalCount="_page.TotalCount" PageNumber="_page.Page"
           PageSize="_page.PageSize" Loading="_loading" SortBy="_sort" SortDescending="_desc"
           OnPageChanged="GoToPage" OnSortChanged="Sort" OnSearch="Search"
           OnRowClick="@(r => Nav.NavigateTo($"/incidents/{r.Id}"))">
    <Toolbar>
        <div class="segmented"> <button class="active">My open</button> <button>Unassigned</button> ... </div>
    </Toolbar>
    <Columns>
        <DataColumn TItem="IncidentRow" Title="Number" SortKey="Number"><Template><span class="cell-link">@context.Number</span></Template></DataColumn>
        <DataColumn TItem="IncidentRow" Title="Priority" SortKey="Priority"><Template><PriorityBadge Priority="context.Priority" /></Template></DataColumn>
        <DataColumn TItem="IncidentRow" Title="State"><Template><StatusPill Text="@context.State" /></Template></DataColumn>
        <DataColumn TItem="IncidentRow" Title="Short description" Value="r => r.ShortDescription" Class="cell-primary" />
    </Columns>
</DataTable>
```

**Record page**: `PageHeader` with breadcrumbs (`<a href="/incidents">Incidents</a> <Icon Name="chevron-right" Size="12"/> INC0001001`),
number + `StatusPill` + `PriorityBadge` in `SubtitleContent`, primary actions in `Actions`; below a `StateFlow`; then
`<div class="grid grid-sidebar">` with the main `card` (form sections using `form-grid`) and a sticky side column
(`sticky-top`) with key facts (`kv` list), SLA progress (`progress`), people (`person` + `Avatar`). Under the form:
`Tabs` (Activity / Related / Attachments / History).

## Components

| Component | Use |
|-----------|-----|
| `Icon Name="…" Size="18"` | All icons. Names in `Components/UI/IconPaths.cs`. |
| `PageHeader` | Title, subtitle, icon, breadcrumbs, actions. |
| `StatCard` | KPI tile – `Tone` primary/success/warning/danger/info/accent, `Delta`, `Trend` sparkline, `OnClick`. |
| `DataTable` + `DataColumn` | Server-paged tables with search (debounced), sort, selection, bulk actions, empty/loading states. |
| `Modal` / `ConfirmDialog` | Dialogs; use `ConfirmDialog Danger="true"` for deletes. |
| `Drawer` | Side panel for details / quick edit / configuration. |
| `Tabs` (`TabItem`) | Page sections. |
| `FormField` | Label + input + help/error. Inputs use classes `input`, `select`, `textarea`. |
| `Switch` | Boolean toggles. |
| `StatusPill` | State pills; tone inferred from common state names or set via `Tone`. |
| `PriorityBadge` | P1–P5 badge with a solid urgency chip. |
| `Avatar` | Initials avatar on a soft tint with a deterministic hue. Use `person` wrapper for name + avatar. |
| `RelativeTime` | "5 min ago" with absolute tooltip. |
| `EmptyState`, `Skeleton`, `spinner` | Empty, loading states. |
| `PermissionView` | Hide UI the user cannot use (backend still enforces!). |
| `LineChart`, `BarChart`, `DonutChart`, `Sparkline` | SVG charts (`ChartSeries`, `ChartSlice`). |
| `UserPicker`, `GroupPicker`, `CiPicker`, `ReferencePicker` | Reference fields with search. |
| `ActivityStream` | Comments / work notes / field changes of a record. |
| `AttachmentList` | Upload / download / delete attachments. |
| `ConditionBuilder` | Edit `ConditionGroup` (filters, rules, flows, SLAs, notifications, reports). |
| `StateFlow` | State progression bar. |
| `ToastService` | `Toasts.Success("Saved")`, `Toasts.FromResult(result, "Saved")`. |

## CSS vocabulary (most used)

Layout: `grid grid-2|grid-3|grid-4|grid-auto|grid-sidebar`, `stack`, `flex items-center gap-2`, `mt-4`, `mb-6`.
Cards: `card`, `card-header`, `card-body`, `card-footer`, `card interactive`, `card highlight`, `card tint` (mint hero or
section), `icon-tile` (`sm|lg`; mint tile with a brand icon). Section headings: `section-title`.
Buttons: `btn btn-primary|btn-secondary|btn-ghost|btn-danger|btn-success|btn-outline-danger`, sizes `btn-sm|btn-lg`,
`btn-icon`, `icon-btn`, `btn-group`, `segmented`.
Forms: `form-grid` (`cols-3`, `span-2`, `span-all`), `field`, `input`, `select`, `textarea`, `input-icon`, `checkbox`,
`choice-card` (radio/checkbox card; `selected` or a checked input gives the brand border + mint fill), `step-badge`
(numbered form section), `form-section`, `form-section-title`, `form-actions`, `readonly-value`. Labels sit above the
input; required fields use `class="required"` (renders `*`).
Text: `muted`, `faint`, `small`, `tiny`, `mono`, `eyebrow` (small uppercase brand label), `link-arrow` ("Request →"
links), `truncate`, `sr-only`. `gradient-text` is a legacy alias for brand-coloured text.
Tables: `table` with `cell-primary`, `cell-sub`, `cell-link`, `cell-item` (icon tile + title + number).
Data: `kv` (definition list), `progress` (`success|warning|danger`), `pill pill-*`, `badge`, `tag`, `alert alert-*`,
`delta up|down|flat`, `live-dot`, `code-block`, `secret-reveal`.

## Rules

- Every user action gives feedback (toast, inline validation, disabled+spinner while busy).
- Destructive actions require `ConfirmDialog`.
- Never render user HTML unencoded. Markdown must go through a safe renderer.
- Keep pages responsive (grid classes collapse automatically below 1280/1024/720 px).
- Use realistic, helpful empty states with a call to action.
- Light theme first; verify the dark theme (`[data-theme="dark"]`) and at least one non-default accent colour too.
- Accessibility: text meets WCAG AA (use the text tokens, never lighter greys); every control keeps a visible focus
  outline (`:focus-visible` is styled globally – never remove it without a replacement); icon-only buttons need
  `title`/`aria-label`; form inputs need a `<label>`.
