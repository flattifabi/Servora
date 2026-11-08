# Configuration module (T-03)

Setup wizard, system settings, branding, feature flags, outgoing e-mail (SMTP + outbox), secrets administration and
the database information card. Everything is configured in the UI; no configuration file has to be edited.

| Area | UI | Permission |
|------|----|-----------|
| Setup wizard | `/setup` (only while no database is configured) | loopback client or setup token |
| System settings | `/admin/settings` – tabs General, Branding, Localization, E-mail, Security, Features, Database | `admin.settings.manage` |
| Database card | `/admin/settings/database` | `admin.database.manage` (tab hidden otherwise) |
| Mail outbox | `/admin/settings/outbox` | `admin.settings.manage` |
| Secrets | `/admin/secrets` | `admin.secrets.manage` |
| Logo | `GET /branding/logo` (anonymous, used on the sign-in page) | – |

Backups, restore and the migration history are **not** part of this module; they live in the backup center
(`/admin/backups`, T-15). The database card only links there.

## Setup wizard

`Welcome → Database → Organisation → Review → Install`, rendered in `AuthLayout`.

- **Database**: provider cards (SQLite, SQL Server, PostgreSQL), connection builder fields or a raw connection string
  (`ConnectionStringComposer`), and *Test connection* (15 s timeout). *Continue* is only enabled once the exact
  connection string shown has been tested successfully. Relative SQLite paths are placed in the data directory.
- **Organisation**: company name, product name, accent color (swatches, color picker or hex), time zone – stored as
  system settings (`branding.*`, `localization.time_zone`).
- **Install** (`SetupService.InstallAsync`, one installation at a time):
  1. verify the connection, 2. create the schema with the *candidate* settings (migrations when present, otherwise
  `EnsureCreated`; foreign databases are refused), 3. save the encrypted `database.json` through
  `DatabaseSettingsStore`, 4. run `DatabaseInitializer` (roles, `admin` / `admin` with forced password change, module
  seeders), 5. apply the organisation settings and audit `setup.completed`, 6. delete the setup token.
  Failures before step 3 leave nothing behind; failures after step 3 can be retried with the same settings.
  `SetupState.IsInProgress` keeps `/setup` reachable while the installation finishes.
- **Remote access**: while unconfigured, `SetupTokenInitializer` writes a random 192-bit token to
  `<data dir>/setup-token.txt` at startup. Clients that are not on the loopback interface (or that arrive through a
  proxy, detected by `X-Forwarded-*` / `Forwarded` headers) must enter it; comparison is constant time, wrong attempts
  are delayed and limited to 10 per circuit. The token is never accepted from the query string.
- All wizard operations go through the circuit-scoped `SetupSession`, which enforces the access rule server-side.

## System settings

`ISystemSettingsService` provides typed settings with defaults, categories, validation and change history:

- Definitions come from `ISettingDefinitionProvider`s (built-ins: `CoreSettingDefinitionProvider`). Modules can add
  their own provider (`services.AddSingleton<ISettingDefinitionProvider, MyProvider>()`).
- Value types (`SettingValueType`): string, multi-line text, integer (min/max), boolean, choice, e-mail, URL
  (http/https only), color (`#rgb` / `#rrggbb`), time zone (system id). `SettingValueParser` normalizes and validates.
- Only values that differ from the default are stored (`sys_properties`). Reads are cached (memory cache) and the
  cache is invalidated on change, so settings take effect **without restart**. Definitions flagged
  `RequiresRestart` show a "Restart required" badge.
- `SetManyAsync` validates everything first and stores all or nothing.
- Every change is audited (`configuration` category, old → new value) and shown in the *Change history* drawer.
- Typed getters (`GetAsync<T>`, `GetStringAsync`, `GetValuesAsync`) are unrestricted for backend use – settings never
  contain secrets. Listing and all mutations require `admin.settings.manage`.

Built-in keys: `general.base_url`, `general.support_email`, `general.default_page_size`, `general.login_banner`,
`branding.product_name`, `branding.company_name`, `branding.accent_color`, `localization.time_zone`,
`localization.culture`, `localization.date_format`, `localization.time_format`, `localization.first_day_of_week`,
`email.*` (see below) and `features.*`.

## Feature flags

Flags are boolean settings with the prefix `features.` (`IFeatureFlags.IsEnabledAsync(key)`). Built-in flags:
`self_service_portal`, `ai_assistant`, `mcp_server`, `rest_api`, `knowledge_feedback`, `email_notifications`.
Administrators can create custom flags (`features.<lower_snake_case>`) and delete them; built-in flags can only be
switched or reset.

## Branding

`IBrandingService.GetAsync()` returns product name, company name, accent color and the logo URL (never throws; returns
defaults while unconfigured). The logo upload accepts PNG, JPEG, GIF and WebP up to 512 KB, detected from magic bytes
(`LogoImageValidator`; SVG is refused because it can carry script). The file is stored as
`<data dir>/branding/logo.bin`, served from `/branding/logo` with `Content-Security-Policy: sandbox`, an ETag and
a one-day cache lifetime.

**Where branding is shown (T-13):** sidebar and sign-in hero (logo, product name, company name), portal header and
footer, every page title (`<page> · <product name>` via the `BrandingState` snapshot, refreshed by the layouts, the
branding panel and at startup) and the accent color (`--primary`, only a validated `#rrggbb` different from the
default). The optional **sign-in notice** (`general.login_banner`) is shown as plain, encoded text on the sign-in page.

## E-mail

- **Settings** (`IEmailConfigurationService`): delivery on/off, host, port, security (`StartTls` or `None`; implicit
  TLS on port 465 is not supported by System.Net.Mail), user name, from address/name, timeout (5–300 s), maximum
  delivery attempts. The password is write-only and stored in `ISecretStore` as `email.smtp_password`; it is never
  returned to the UI. `SmtpSettingsValidator` rejects incomplete settings and credentials over an unencrypted
  connection to a non-local server.
- **Sending** (`IEmailSender` → `QueuedEmailSender`): recipients are validated and de-duplicated (max 100), subjects are
  folded to one line, body sizes are limited, and the message is written to the outbox (`OutboundEmail`); `MailOutboxSignal` wakes the `MailOutboxProcessor` immediately.
- **Outbox processor** (hosted service): claims due messages in batches, sends them through `ISmtpTransport`
  (`SmtpTransport`, System.Net.Mail, async with a hard timeout), retries with back-off 1 min, 5 min, 15 min, 1 h, then
  every 4 h until the attempt budget is used, recovers messages stuck in "sending" after 10 minutes and purges sent
  messages after 30 days. SMTP errors are stored without credentials.
- **Test e-mail**: sent immediately with the saved settings (bypassing the queue) so the server's answer is shown;
  recorded in the outbox with source `test`.
- **Outbox UI**: KPIs, status filter, search, message preview, retry, "retry all failed", delete.

## Secrets

`ISecretAdminService` lists secrets (name, description, whether a value is set, last rotation, platform usage) – values
are never returned. Create and rotate take a write-only value (max 8,000 characters); names are 3–200 lower-case
characters, digits, `.`, `_` or `-` (`SecretNameRules`). Secrets with platform prefixes (`email.`, `ldap.`, `ai.`, `datasource.`, `integration.`, `mcp.`)
are flagged "Platform". Deleting requires confirmation. Every action is audited without the value.
`StoredSecret.ProtectedValue` carries `[NotExposed]`.

## Database card

`IDatabaseAdminService` shows provider, server version, database / data source, size (SQLite file + WAL), where the
configuration comes from, the redacted connection string and a one-line schema status, plus an audited *Test
connection*. Migrations and backups: `/admin/backups`.

## Extension points

| Contract | Use |
|----------|-----|
| `ISystemSettingsService` | Read typed settings anywhere in the backend. |
| `ISettingDefinitionProvider` | Contribute settings of another module. |
| `IFeatureFlags` | Check `features.*` flags. |
| `IBrandingService` | Product name, company, accent color and logo for layouts and e-mails. |
| `IEmailSender` | Queue an e-mail (returns `false` when delivery is disabled or the message is invalid). |

## Tests

`tests/Servora.Tests.Unit/Configuration/`: setting parsing and defaults, settings service (caching, all-or-nothing,
feature flags, permissions), SMTP option validation, outbox retry policy, queued sender, test e-mail, real
SMTP transport against a loopback SMTP server, branding/logo validation, secrets administration, connection string
composer and setup access rules (loopback/proxy detection, token validation).
