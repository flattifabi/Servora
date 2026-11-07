# Servora

**Servora** is an open-source, self-hosted enterprise service management platform – a free alternative to
ServiceNow built with **.NET 10 and Blazor**. Incidents, problems, changes, service catalog & requests, knowledge,
CMDB, assets, no-code automation, REST integrations, analytics, an MCP server and AI agents – configured entirely in
the browser.

> Status: active development (0.x). See [`agent/TASKS.md`](agent/TASKS.md) for the live task board.

## Highlights

- **ITSM suite** – Incident, Problem, Change (CAB approvals), Service Catalog, Requests, Approvals, SLAs.
- **Knowledge, CMDB & Assets** – articles with review workflow, CI relationships with dependency graph, asset lifecycle.
- **Platform studio** – custom tables & fields, business rules, notifications – all without code.
- **Automation** – visual Flow Designer, schedules, approvals, SSRF-safe REST data sources, import sets.
- **Identity** – local accounts, LDAP / Active Directory, Windows single sign-on, RBAC with groups and AD mappings.
- **Integration** – REST API with API keys, OpenAPI, MCP server for AI clients, AI assistant with scoped sub-agents.
- **Operations** – Windows Service, installer/updater, live log viewer with debug switch, tamper-evident audit log,
  health dashboard, analytics and report builder.
- **Databases** – SQLite, Microsoft SQL Server, PostgreSQL.

## Quick start (development)

```bash
dotnet run --project src/Servora.Web
```

Open http://localhost:5080 and sign in with **admin / admin** – you will be asked to choose a new password
immediately. Development uses SQLite in `src/Servora.Web/App_Data` (see `appsettings.Development.json`).
To try the first-run setup wizard, use the launch profile `Servora (setup wizard)`.

## Installation (production)

Download `ServoraSetup-<version>-win-x64.exe` from GitHub Releases and run it as administrator. It installs Servora as the
Windows Service `Servora` or updates an existing installation in place (with backup and rollback). See
[`docs/operations/installation.md`](docs/operations/installation.md).

## Repository layout

```
src/
  Servora.Domain/              Entities, enums, domain rules (no dependencies)
  Servora.Application/         Use cases, services, contracts, DTOs
  Servora.Infrastructure/      EF Core, LDAP/AD, SMTP, HTTP, crypto, background services
  Servora.Persistence.*/       SQLite / SQL Server / PostgreSQL providers + migrations
  Servora.Web/                 Blazor UI, REST API, MCP endpoint, Windows Service host
  Servora.Installer/           Installer / updater for the Windows Service
tests/                         Unit and integration tests
docs/                          Architecture, development, modules, operations
agent/                         Orchestration protocol, task board, decisions
```

## Documentation

- [Architecture overview](docs/architecture/overview.md)
- [UI guidelines & component catalog](docs/development/ui-guidelines.md)
- [Orchestration protocol](agent/ORCHESTRATION.md) · [Task board](agent/TASKS.md) · [Decisions](agent/DECISIONS.md)
- [Operations: installation](docs/operations/installation.md) · [upgrade](docs/operations/upgrade.md) · [Windows service](docs/operations/windows-service.md) · [backup & restore](docs/operations/backup-restore.md) · [configuration](docs/operations/configuration.md) · [troubleshooting](docs/operations/troubleshooting.md)
- [Product specification](PROJECT_SPEC.md)

## License

[MIT](LICENSE) – free for commercial and non-commercial use.
