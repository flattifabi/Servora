# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Platform kernel: clean architecture solution (.NET 10), EF Core persistence for SQLite / SQL Server / PostgreSQL,
  audit fields, optimistic concurrency, before-save hooks and asynchronous record events.
- Authentication & authorization: cookie sign-in, lockout, password policy, forced password change for the bootstrap
  `admin` account, security-stamp revalidation, RBAC with permission catalog and dynamic policies.
- Hash-chained audit log, encrypted secret store (Data Protection), number sequences, activity stream, attachments.
- Premium design system with dark/light themes, app shell, command palette (Ctrl+K), toasts and 30+ components
  including SVG charts.
- Windows Service hosting and setup guard for first-run configuration.
- `ServoraSetup.exe` installer/updater (install, update with backup and automatic rollback, uninstall, status), hardened
  directory ACLs, virtual service account, GitHub CI and release workflows, `build/publish.ps1`, operations documentation.
