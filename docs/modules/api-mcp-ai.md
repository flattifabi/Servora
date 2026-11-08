# API Keys, REST API, MCP Server & AI (T-10)

This module gives Servora a machine interface (API keys, REST conventions, OpenAPI, API Explorer), a Model Context
Protocol server for AI clients, and a provider-neutral AI layer (providers, orchestrator with sub-agents, assistant).
Everything is implemented in-house with `HttpClient` and `System.Text.Json` – no SDK packages (ADR-010, ADR-012).

## Tables

| Table | Entity | Purpose |
|-------|--------|---------|
| `sys_api_key` | `ApiKey` | Key metadata: public key id, **SHA-256 hash** of the secret (`[NotExposed]`), owner user *or* service identity, scopes, expiry, allowed IP ranges, rate limit, revocation / rotation / last use. |
| `sys_api_key_usage` | `ApiKeyUsageDay` | Requests and rejected attempts per key and UTC day (usage chart). Kept 400 days. |
| `sys_mcp_tool` | `McpToolSetting` | Per-tool enable switch (missing row = enabled). |
| `sys_mcp_invocation` | `McpInvocation` | One row per MCP request (method, tool/resource, outcome, duration, key, client). No arguments or results are stored. Kept 30 days. |
| `ai_provider` | `AiProviderConfig` | Provider settings (kind, base URL, model / deployment, limits, default flag, last test). The API key lives in `ISecretStore` under `ai.provider.<id>.apikey`. |
| `ai_agent` | `AiAgentSetting` | Per-agent enable switch. |
| `ai_conversation`, `ai_message` | `AiConversation`, `AiMessage` | Assistant conversations (private to their user) and messages incl. token counts. |
| `ai_tool_call` | `AiToolCall` | Execution trace: delegations, tool calls, proposed actions with their confirmation state. |

The MCP server switch is the system setting `mcp.server.enabled` (default on).

## API keys (`/admin/api-keys`, permission `admin.apikeys.manage`)

- **Format** `srv_<key id>_<secret>`: 12 lower-case alphanumerics (stored in clear for lookup) + 40 alphanumerics
  (~238 bits). Only `SHA-256(secret)` is stored; verification uses `CryptographicOperations.FixedTimeEquals`.
  The token is displayed **once** after create / rotate (`SecretRevealDialog`) and can never be retrieved again.
- **Scopes** are permission keys from the catalog plus `mcp.tool.<name>` / `mcp.tool.*`. A creator can only grant
  scopes covered by their own permissions (`ApiKeyScopePolicy`); tool scopes require `mcp.access`. Only signed-in
  users can create, change or rotate keys – a key can never mint keys.
- **Owner**: a user (keys for other users need `admin.users.manage`) or a named service identity. For user-owned
  keys every permission check requires the scope **and** the owner's current permission (`CurrentUser`), and keys of
  deactivated owners stop working.
- **Restrictions**: expiry, allowed IP addresses / CIDR ranges (IPv4-mapped IPv6 handled), per-key rate limit
  (requests per minute, enforced by the global rate limiter for keys that authenticated before; unknown key ids fall
  into the per-IP limit of 300/min).
- **Lifecycle**: create, edit, rotate (old token stops immediately), revoke (permanent). Every lifecycle event is
  audited (`security` category); failed authentications are audited as `apikey.auth_failed`, throttled to one event
  per reason and key per minute. The authenticator caches key data for 30 s and drops the cache on every change.
- **Usage**: success / failure counters are aggregated in memory and flushed every 10 s by
  `ApiUsageMaintenanceService` (also updates `LastUsedAt`, `LastUsedIp`, `UsageCount` and purges old rows).

### Authentication scheme

`ApiKeyAuthenticationHandler` (scheme `ApiKey`) reads `X-Api-Key: srv_…` or `Authorization: Bearer srv_…` and creates
a principal with `actor_type=apikey`, `api_key_id`, one `permission` claim per scope and – for user-owned keys – the
owner's id as `NameIdentifier`. A policy scheme (`Servora`) is the default: `/mcp` always uses API keys,
`/api` and `/openapi` use the key when one is sent and the auth cookie otherwise (API Explorer "try it"), the UI
uses the cookie. Challenges / forbids are answered with RFC 7807 problem details.

## REST API foundation

- Base path `/api/v1`; JSON only; errors as `ProblemDetails` with `correlationId`; every API response carries
  `X-Correlation-Id`; unhandled exceptions become a 500 problem (no stack traces); unknown `/api/**` paths return a
  JSON 404; HTML status code pages are disabled for API paths. Lists use `page` / `pageSize` (max 100,
  `ApiPagedResponse<T>`).
- Core endpoints: `GET /api/v1/health` (anonymous, database reachability) and `GET /api/v1/me` (requires `api.access`).
  Modules add their endpoints to their own `*Endpoints.cs`; anything under `api/` is included in the document.
- OpenAPI 3.1 document: `GET /openapi/v1.json` (authenticated) via `Microsoft.AspNetCore.OpenApi`, enriched by
  `ServoraOpenApiDocumentTransformer` (API key security scheme, title, description).
- **API Explorer** `/admin/api-docs` (`api.access`): reads the OpenAPI document, lists operations by tag with search,
  parameters, responses, curl snippets and a "try it" panel that calls the API with the user's own session.

### Error responses of the API surface (T-13)

`/api/**`, `/mcp` and `/openapi/**` never answer with HTML: cookie challenges (expired session), forbids, the forced
password change (`403`, title *Password change required*), empty error statuses (404, 405, 503 during setup, …) and
unhandled exceptions are returned as `application/problem+json` with the correlation id, independent of the `Accept`
header (`ApiProblemWriter`, `ApiAwareCookieRedirects`). The UI keeps the HTML not-found / error pages. Previously an
unauthenticated cookie `POST` was re-executed against the Razor "not found" page, which failed with *400 invalid
content type*.

## MCP server (`POST /mcp`)

- Transport: **Streamable HTTP**, JSON-RPC 2.0, single messages and batches (max 20), request body max 1 MB, JSON
  depth max 32. Responses are `application/json`; notifications return `202 Accepted`. `GET`/`DELETE` return 405 (no
  server-initiated streams, no sessions). Cross-origin requests (an `Origin` header for another host) are rejected.
- **Supported protocol revisions**: `2025-11-25` (latest), `2025-06-18`, `2025-03-26`, `2024-11-05`. `initialize`
  echoes the client's revision when supported and otherwise answers with the latest one; a request carrying an
  unsupported `MCP-Protocol-Version` header is rejected with 400.
- Methods: `initialize`, `ping`, `notifications/*`, `tools/list`, `tools/call`, `resources/list`,
  `resources/templates/list`, `resources/read`.
- **Authorization**: API keys only (no cookies → no CSRF); the endpoint requires `mcp.access`. `tools/list` shows only
  enabled tools covered by the key's `mcp.tool.<name>` scope **and** the underlying permission; `tools/call` checks both
  again (JSON-RPC error `-32001` when denied). Application services check permissions a third time for the actor.
- **Validation and limits**: arguments are validated against the tool's JSON schema (`JsonSchemaValidator`: type,
  properties, required, additionalProperties, enum, lengths, pattern with regex timeout, uuid / date-time formats,
  numeric ranges, array / object sizes) before execution (`-32602` with an error list). Tool runs time out after
  30 s; text output is capped at 64 KB (structured content is omitted when truncated); search tools return at most 50
  records.
- **Audit**: every `initialize`, `tools/call` and `resources/read` is stored in `sys_mcp_invocation` and written to the
  audit log (without arguments or results).

| Tool | Scope | Underlying permission | Kind |
|------|-------|-----------------------|------|
| `search_records` | `mcp.tool.search_records` | table read permission (checked by `IRecordService`) | read-only |
| `get_record` | `mcp.tool.get_record` | table read permission (checked by `IRecordService`) | read-only |
| `search_knowledge` | `mcp.tool.search_knowledge` | `knowledge.read` | read-only |
| `create_incident` | `mcp.tool.create_incident` | `incident.create` | consequential |
| `list_data_sources` | `mcp.tool.list_data_sources` | `integration.datasources.read` | read-only |
| `execute_data_source` | `mcp.tool.execute_data_source` | `integration.datasources.execute` | consequential |
| `list_flows` | `mcp.tool.list_flows` | `automation.read` | read-only |
| `start_flow` | `mcp.tool.start_flow` | `automation.execute` | consequential |
| `get_flow_run` | `mcp.tool.get_flow_run` | `automation.read` | read-only |
| `platform_health` | `mcp.tool.platform_health` | `admin.health.read` | read-only |

Resources: `servora://server/info` (always), `servora://tables` and `servora://tables/{name}` (table metadata,
requires `platform.records.read`). Record data is only available through the tools.

Tools are thin adapters over kernel contracts (`IRecordService`, `IDataSourceExecutor`, `IFlowExecutionService`) and
therefore work with the real implementations of T-07 / T-08 once merged. New tools implement `IMcpTool` (or derive
from `McpToolBase`) and are registered as scoped services; they appear automatically in MCP, the scope picker and the
AI agents that reference them.

Admin page `/admin/mcp` (`admin.mcp.manage`): server switch, KPIs, tool catalog with per-tool switch and input
schema, client snippets (generic Streamable HTTP JSON, Claude Desktop via `mcp-remote`, curl) and recent invocations.

## AI providers (`/admin/ai`, permission `admin.ai.manage`)

| Kind | Endpoint | Auth header |
|------|----------|-------------|
| OpenAI-compatible (OpenAI, Ollama, LM Studio, vLLM, …) | `<base>/chat/completions` | `Authorization: Bearer` (optional for local servers) |
| Anthropic | `<base>/v1/messages` (`anthropic-version: 2023-06-01`) | `x-api-key` |
| Azure OpenAI | `<base>/openai/deployments/<deployment>/chat/completions?api-version=…` | `api-key` |

Per provider: model / deployment, max tokens, temperature, timeout (5–600 s), tool calling on/off, enabled, default.
API keys are stored only in `ISecretStore`, never returned (the UI shows "configured"), redacted from provider error
messages and excluded from `ToString()`. Responses are limited to 4 MB, redirects are not followed. The **Test**
button sends a short prompt and records latency and result.

## Orchestrator and sub-agents

`IAiOrchestrator` (`AiOrchestrator`) receives the user message and the recent history:

1. **With tool calling** the orchestrator model gets one `delegate_to_<agent>` tool per enabled agent (max 3 rounds).
   Each delegation runs the agent with its own tools (max 4 rounds) and returns the agent's answer to the orchestrator,
   which writes the final answer.
2. **Without tool calling** `AiHeuristicPlanner` routes by keywords; the agent prefetches read-only data
   (`BuildPrefetch`, e.g. a knowledge search) and answers in one completion; general questions are answered directly.

| Agent | Tools | Maximum scope |
|-------|-------|---------------|
| Triage (`triage`) | `search_records`, `create_incident` | `incident.read`, `incident.create`, `platform.records.read` |
| Knowledge (`knowledge`) | `search_knowledge` | `knowledge.read` |
| Record (`records`) | `search_records`, `get_record` | read permissions of the ITSM / CMDB / asset / knowledge / user tables, `table.*` |
| Automation (`automation`) | `list_flows`, `start_flow`, `get_flow_run` | `automation.read`, `automation.execute` |

**Security model**: every agent run gets a new DI scope whose `ActorContext` is an `ActorInfo` of type `Agent` with the
permissions `agent scope ∩ user permissions` (`AiPermissionIntersection`, wildcard-aware) – an agent can never do more
than the user. Tool arguments are schema-validated. **Consequential tools are never executed by the model**: the call
is stored as a *pending action* and the agent is told that nothing happened; the user confirms or rejects it in the
UI. Confirmation re-validates the arguments, re-checks the permission under the agent identity and claims the action
atomically (a pending action can be executed at most once). Every step (delegation, tool call, action) is persisted
in `ai_tool_call` with agent, identity, status, duration and bounded arguments / results.

## AI Assistant (`/assistant`, permission `ai.assistant.use`)

Default role grants for `api.access`, `mcp.access` and `ai.assistant.use` are documented in [identity.md](identity.md#built-in-roles-and-default-grants-t-13).

Chat UI with conversation history (search, delete), suggestions, progressive rendering of answers, safe Markdown
subset (bold, inline code, code blocks, lists, quotes, headings – everything HTML-encoded), tool-call cards showing
which agent used which tool under which identity, confirmation buttons for proposed actions, provider / agent status
and a guided empty state when no provider is configured. Messages are limited to 4,000 characters; the last 12
messages are sent as context.

## Tests

`tests/Servora.Tests.Unit/Api`, `/Mcp`, `/Ai`: token generation / hashing / constant-time verification, scope policy,
IP ranges, key lifecycle on SQLite (create, authenticate, rotate, revoke, expiry, IP restriction, deactivated owner,
escalation), MCP JSON-RPC dispatch (initialize / version negotiation, scopes, permissions, schema validation, batches,
notifications, errors, disabled server, resources), JSON schema validator, provider wire formats against a stub
HTTP endpoint, orchestrator delegation / identities / confirmations with a fake provider, and the assistant end to end
(configured OpenAI-compatible endpoint, confirm-once, privacy of conversations).

## Known limitations

- MCP: no server-sent events / sessions (not required for request–response tools), no `prompts`, no resource
  subscriptions, no OAuth (API keys only).
- Assistant answers are rendered progressively after the provider returned them; true token streaming from the
  provider is not implemented yet.
- `search_records`, `get_record`, `search_knowledge`, `create_incident` depend on the real `IRecordService` (T-07); the
  data source and flow tools depend on T-08. With the kernel placeholders they return "not available" results.
