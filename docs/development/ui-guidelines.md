# UI Guidelines & Component Catalog

Servora must look and feel like premium enterprise software. Use these building blocks – never hand-roll colors,
spacing or ad-hoc widgets. All styles live in `src/Servora.Web/wwwroot/css/servora.css`; module-specific styling goes
into scoped `*.razor.css` files.

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
| `PriorityBadge` | P1–P5 badge (P1 glows). |
| `Avatar` | Initials avatar with deterministic gradient. Use `person` wrapper for name + avatar. |
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
Cards: `card`, `card-header`, `card-body`, `card-footer`, `card interactive`, `card highlight`, `card glass`.
Buttons: `btn btn-primary|btn-secondary|btn-ghost|btn-danger|btn-success|btn-outline-danger`, sizes `btn-sm|btn-lg`,
`btn-icon`, `icon-btn`, `btn-group`, `segmented`.
Forms: `form-grid` (`cols-3`, `span-2`, `span-all`), `field`, `input`, `select`, `textarea`, `input-icon`, `checkbox`,
`form-section`, `form-section-title`, `form-actions`, `readonly-value`.
Text: `muted`, `faint`, `small`, `tiny`, `mono`, `eyebrow`, `gradient-text`, `truncate`.
Data: `kv` (definition list), `progress` (`success|warning|danger`), `pill pill-*`, `badge`, `tag`, `alert alert-*`,
`delta up|down|flat`, `live-dot`, `code-block`, `secret-reveal`.

## Rules

- Every user action gives feedback (toast, inline validation, disabled+spinner while busy).
- Destructive actions require `ConfirmDialog`.
- Never render user HTML unencoded. Markdown must go through a safe renderer.
- Keep pages responsive (grid classes collapse automatically below 1280/1024/720 px).
- Use realistic, helpful empty states with a call to action.
- Dark theme first, verify light theme too (`[data-theme="light"]`).
