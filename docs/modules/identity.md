# Identity & Access (T-02)

Users, groups, roles and permissions, LDAP / Active Directory integration, Windows integrated sign-in, the
authentication policy and the self-service profile. Servora works fully **without** a directory (local accounts);
the directory and Windows sign-in are optional and configured only through the UI.

## Pages

| Route | Permission | Purpose |
|-------|-----------|---------|
| `/admin/users` | `admin.users.read` | KPI cards (total, active, directory / Windows, locked out, signed in during the last 30 days), filters status (active / inactive / locked) / source / role, server-side search, sort and paging. |
| `/admin/users/new`, `/admin/users/{id}` | read; changes need `admin.users.manage` | Profile fields, activation, roles (direct and inherited through groups), groups, effective permissions, **reset password** (generated or typed, always sets *must change password*), **unlock**, **force sign-out** (rotates the security stamp), delete. Source badge Local / Directory / Windows / Service. |
| `/admin/groups`, `/admin/groups/{id}` | read; changes need `admin.groups.manage` | Type (assignment / security / approval), manager, e-mail, parent, members (add / remove), roles granted to all members. |
| `/admin/roles`, `/admin/roles/{id}` | read; changes need `admin.roles.manage` | Permission matrix grouped by `PermissionCatalog` area with search, wildcard keys (`incident.*`, `*`), holders and groups. |
| `/admin/directory` | `admin.directory.manage` | LDAP / AD connection, attribute mapping, group → role / group mappings, provisioning, scheduled sync + "Sync now" with history, Windows sign-in switch, "Test connection" and "Test user lookup". |
| `/admin/auth` | `admin.auth.manage` | Password rules, lockout threshold / duration, session lifetime, "keep me signed in" lifetime, allow local sign-in. |
| `/profile` | signed-in user | Own name, phone, time zone, theme; my roles and groups; session facts; sign out everywhere; change password link. |
| `/login`, `/account/change-password` | anonymous / signed-in | Restyled; the Windows button is shown only when Windows sign-in is enabled; password help text follows the policy. |
| `GET /auth/windows` | anonymous | Negotiate challenge → maps the Windows identity → normal application cookie. |

## Application services

| Contract | Highlights |
|----------|-----------|
| `IUserAdminService` | List / stats / get / create / update (optimistic concurrency), activate / deactivate, reset password, unlock, force sign-out, delete, grant / revoke roles. |
| `IGroupAdminService` | List / stats / get / create / update / delete, add members (bulk, ≤ 200), remove member, grant / revoke roles. |
| `IRoleAdminService` | List, options for pickers, get, create, update (permission set), delete. |
| `IDirectoryAdminService` | Settings (bind password via `ISecretStore`), connection test, user lookup test, mappings, sync now, sync history. |
| `IAuthPolicyAdminService` / `IAuthPolicyProvider` | Policy editing / cached reading (`identity.auth.policy` system property). |
| `IProfileService` | Own profile, update, sign out everywhere. |
| `IWindowsSignInService` | Windows identity → Servora user mapping and JIT provisioning. |
| `IDirectoryClient` | `System.DirectoryServices.Protocols` implementation (`LdapDirectoryClient`). |
| `IDirectoryProvisioningService`, `IDirectorySyncService` | JIT provisioning / reconciliation and bounded synchronisation runs. |

Every service method checks `ICurrentUser.HasPermissionAsync(...)` and returns `Result.Forbidden()` otherwise; the
pages additionally carry `[RequirePermission]`. Every change is audited (`identity.*` and `auth.*` actions, never
with secrets) and invalidates the `IPermissionService` cache of the affected users. Changes that reduce a user's
access (deactivation, password reset, force sign-out, role / group removal) rotate the security stamp so open
sessions and Blazor circuits are revalidated immediately (`IUserSessionInvalidator`).

## Security rules

* **Privilege-escalation guard** (`PrivilegeEscalationGuard`): an actor can only grant permissions it holds itself –
  when editing a role's permissions, granting a role to a user or group, adding members to a group that grants roles,
  and when mapping a directory group to a role. Coverage follows `PermissionMatcher` semantics (`*` only by `*`,
  `incident.*` by itself or a broader wildcard). Additionally an actor can only manage accounts whose effective
  permissions it fully holds (no resetting the administrator's password with `admin.users.manage` alone).
* **System roles** (`IsSystem`) cannot be deleted and keep their technical name; the `admin` role always keeps `*`.
* **Last administrator** (`SuperAdminGuard`): the last active user holding `*` cannot be deactivated, deleted or
  stripped of the role, and cannot lose it through group changes.
* **Break-glass**: with "allow local sign-in" off, only directory / Windows accounts sign in – except the bootstrap
  `admin` account (ADR-006).
* Demo users are seeded with 32-character random passwords that are never shown, plus *must change password*.

## LDAP / Active Directory

* **Transport**: LDAPS (default, port 636), StartTLS or None (UI warning). A failed StartTLS never falls back to plain
  LDAP. Certificate validation can only be switched off explicitly (UI warning, Windows only).
* **Binds**: a service account (bind DN + password from `ISecretStore`, secret `identity.ldap.bind_password`) is
  required – anonymous binds are refused. User passwords are verified by a second bind with the user's DN; empty
  passwords are rejected before any bind (prevents unauthenticated-bind bypass).
* **Filters**: every user-supplied value is escaped per RFC 4515 (`LdapFilter`); configured base filters are
  validated for balanced parentheses. Login names accept `user`, `DOMAIN\user` (domain must match the configured
  NetBIOS domain) and `user@domain` (UPN attribute).
* **Bounded**: connection / operation timeouts, size limits on every search, `MaxSyncEntries` for synchronisation,
  referral chasing off, nested groups resolved with a bounded matching-rule-in-chain query when enabled.
* **Provisioning**: JIT on first sign-in (optional); users matched by external id (objectGUID / entryUUID) first,
  then by name for directory accounts only – a local account with the same name is **never** linked
  (`directory.link_refused`). Disabled directory accounts are deactivated and not re-activated automatically.
* **Group mappings** (`DirectoryGroupMapping`): a directory group (full DN or common name) grants a role and / or a
  local group membership. Assignments created by mappings carry `IsFromDirectory = true` and are reconciled on every
  sign-in and sync; manual assignments are never touched.
* **Synchronisation**: `DirectorySyncHostedService` checks every minute and runs when the configured interval has
  elapsed; "Sync now" runs immediately. Runs are stored in `DirectorySyncRun` (history on the page) and audited.
  Options: import new users, deactivate users missing from the directory.

Settings live in the system property `identity.directory.settings` (JSON, no secrets).

## Windows integrated sign-in

1. Enable "Windows sign-in" on `/admin/directory` (optionally set the NetBIOS domain and auto provisioning).
2. The login page then shows **Sign in with Windows** → `GET /auth/windows` challenges the `Negotiate` scheme
   (registered in `IdentityWebModule`, never the default scheme).
3. The authenticated identity (`DOMAIN\user` + SID) is mapped: existing Windows account by SID → directory lookup
   when LDAP is enabled (SID must match, disabled accounts refused, JIT provisioning with group mappings) → existing
   directory account by name when the directory is unreachable → otherwise a new *Windows* account (only without
   LDAP, requires the SID, never takes over an existing login name).
4. Local and service accounts can never sign in through Windows. Lockout and deactivation apply.

Kerberos requires an SPN for the service account (`setspn -S HTTP/servora.corp.example.com DOMAIN\svc-servora`) and
the site in the browser's intranet zone; NTLM works without an SPN. Without a domain the challenge fails and the
fallback page links back to the password sign-in.

## Data model additions

| Entity | Table | Notes |
|--------|-------|-------|
| `DirectoryGroupMapping` | `sys_directory_group_map` | Directory group → role and / or local group, active flag. |
| `DirectorySyncRun` | `sys_directory_sync_run` | Trigger, status, counts (read, created, updated, deactivated, membership changes, errors), summary. |

`PasswordPolicy` gained `RequireSymbol` and `Describe()`. No migrations are part of this task (T-13).

## Demo data

`IdentityDemoSeeder` (idempotent, marker `identity.demo_data.seeded`): groups Service Desk, Network Operations,
Database Administration (assignment, role `itil`) and Change Advisory Board (approval); nine demo users (fulfillers
and employees) with memberships and roles, random passwords and *must change password*.

## Tests

`tests/Servora.Tests.Unit/Identity`: LDAP filter escaping (RFC 4515), DN parsing / login names, directory settings
validation, group → role mapping, JIT provisioning and reconciliation, privilege-escalation guard (unit + service
level for roles, groups and users), last-administrator protection, sign-in policy (local sign-in switch,
break-glass admin, lockout threshold), auth policy validation, temporary passwords, SID formatting and Windows
identity mapping.
