# Configuration

Almost everything in Servora is configured in the web UI (administration). This page covers the few host-level
settings that must exist before the application starts: data directory, listen URLs, TLS and reverse proxies.

## Data directory

All runtime data lives outside the program files so that updates can replace the program files safely (ADR-011):

```
C:\ProgramData\Servora\           (data directory)
  appsettings.json                instance settings (this page)
  database.json                   database provider + encrypted connection string (written by the setup wizard)
  servora.db                      SQLite database (when SQLite is used)
  keys\                           ASP.NET Core Data Protection key ring (DPAPI, machine-bound)
  attachments\                    record attachments
  logs\                           application logs, installer transcripts (setup-*.log)
  backups\                        program file backups made by the installer
  setup-token.txt                 one-time token for remote access to the setup wizard (only while unconfigured)
```

Resolution order (same in the host and the installer):

1. Environment variable `SERVORA_DATA_DIR` of the service (`ServoraSetup install --data-dir D:\ServoraData` sets it),
2. machine-wide environment variable `SERVORA_DATA_DIR`,
3. `%ProgramData%\Servora` when running as a service, `.\App_Data` below the content root when running from a console.

The installer restricts the data directory to SYSTEM, Administrators and the service account. Keep it that way: it
contains the encryption keys and, for SQLite, the whole database.

### Moving the data directory

```powershell
Stop-Service Servora
robocopy C:\ProgramData\Servora D:\ServoraData /E /COPY:DATS /XD C:\ProgramData\Servora\backups
$key = 'HKLM:\SYSTEM\CurrentControlSet\Services\Servora'
Set-ItemProperty $key -Name Environment -Type MultiString -Value @('SERVORA_DATA_DIR=D:\ServoraData')
Start-Service Servora
```

(`/COPY:DATS` keeps the restrictive permissions. If the service already has other environment variables, append
instead of replacing – see [windows-service.md](windows-service.md#service-environment-variables).) The installer's
backups stay in `%ProgramData%\Servora\backups`.

## Instance settings (`<data dir>\appsettings.json`)

The host reads configuration in this order; later sources win:

1. `<install dir>\appsettings.json` – shipped defaults, **replaced on every update – do not edit**
2. `appsettings.Production.json` (not shipped)
3. environment variables, command line
4. **`<data dir>\appsettings.json`** – your instance settings; survive updates and win over everything above

The installer creates the file with the chosen port and afterwards only ever changes `Urls` during a fresh
installation. Most settings apply without restart (the file is watched); restart the service after changing listen
URLs or certificates. Configuration keys are case-insensitive; do not define the same key twice with different casing.

Example:

```json
{
  "Urls": "http://0.0.0.0:5080",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

| Key | Default | Purpose |
|-----|---------|---------|
| `Urls` | `http://0.0.0.0:5080` | Listen URLs, `;`-separated. `0.0.0.0` = all IPv4 interfaces, `[::]` = all IPv6, `127.0.0.1` = local only (behind a reverse proxy). |
| `Kestrel:Endpoints:*` | – | Detailed endpoints incl. HTTPS certificates (below). When present, `Urls` is ignored. |
| `AllowedHosts` | `*` | Host header filter, e.g. `servora.example.com;localhost`. Recommended in production. |
| `Logging:LogLevel:*` | `Information` | Log levels (the UI debug switch changes logging at runtime). |
| `Logging:EventLog:LogLevel:Default` | `Warning` | What goes to the Windows Event Log. |
| `Servora:Database:*` | – | Pre-configured database (below). |

### Pre-configuring the database

For unattended deployments the setup wizard's database step can be skipped by configuring the database in the
instance settings before the first start:

```json
{
  "Urls": "http://0.0.0.0:5080",
  "Servora": {
    "Database": {
      "Provider": "SqlServer",
      "ConnectionString": "Server=sql01;Database=Servora;Integrated Security=true;Encrypt=true"
    }
  }
}
```

Providers: `Sqlite` (`Data Source={DataDirectory}/servora.db`), `SqlServer`, `PostgreSql`. Prefer Windows
authentication (`Integrated Security=true` – the service connects as the computer account `DOMAIN\SERVER$`) so that no
password is stored in the file. A connection string entered in the setup wizard is stored encrypted in
`database.json` instead.

## HTTPS with Kestrel

Servora's built-in web server (Kestrel) can terminate TLS itself – recommended when Windows single sign-on is used,
because Kerberos works best without a proxy in between.

### Certificate from the Windows certificate store (recommended)

1. Import the certificate with private key into *Local Computer → Personal* (`certlm.msc`). The subject or a SAN must
   match the host name users type, for example `servora.example.com`.
2. Allow the service account to read the private key: `certlm.msc` → certificate → *All Tasks → Manage Private
   Keys…* → *Add* → enter `NT SERVICE\Servora` (location: the local computer) → grant *Read* only. Repeat this after
   every certificate renewal, because a renewed certificate has a new private key.
3. Configure the endpoints in `<data dir>\appsettings.json`:

   ```json
   {
     "Kestrel": {
       "Endpoints": {
         "Https": {
           "Url": "https://0.0.0.0:443",
           "Certificate": {
             "Subject": "servora.example.com",
             "Store": "My",
             "Location": "LocalMachine",
             "AllowInvalid": false
           }
         },
         "HttpLocal": {
           "Url": "http://127.0.0.1:5080"
         }
       }
     },
     "AllowedHosts": "servora.example.com;localhost"
   }
   ```

   The local HTTP endpoint is optional; it keeps `ServoraSetup` health checks simple (`http://localhost:5080/health`)
   without exposing plain HTTP to the network. Without it, pass `--health-url https://servora.example.com/health`
   to `ServoraSetup update`.
4. Update the firewall rule to the HTTPS port and restart the service:

   ```powershell
   Set-NetFirewallRule -DisplayName Servora -LocalPort 443
   Restart-Service Servora
   ```

### Certificate from a PFX file

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:443",
        "Certificate": {
          "Path": "C:\\ProgramData\\Servora\\certs\\servora.pfx",
          "Password": "<pfx password>"
        }
      }
    }
  }
}
```

Keep the PFX inside the data directory (its permissions already restrict access to SYSTEM, Administrators and the
service account) and never commit or share the file with the password. The store-based variant avoids a password in a
file and is preferred.

Never set `AllowInvalid` to `true` in production and never disable certificate validation in clients – use a
certificate from your internal CA or a public CA instead.

## Reverse proxy

A reverse proxy (IIS with ARR, nginx, Caddy, a load balancer) can terminate TLS in front of Servora:

1. Bind Servora to loopback only, so it is not reachable around the proxy:
   `"Urls": "http://127.0.0.1:5080"` in the instance settings; remove the installer's firewall rule
   (`Remove-NetFirewallRule -DisplayName Servora`) if the proxy runs on the same machine.
2. Forward **WebSockets** – the Blazor UI keeps a WebSocket connection (`/_blazor`). Long idle timeouts (≥ 100 s)
   avoid reconnect banners.
3. Pass the original scheme and client address (`X-Forwarded-Proto`, `X-Forwarded-For`) and let Servora honour them by
   setting the service environment variable `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
   ([how](windows-service.md#service-environment-variables)). This trusts the headers from any sender, which is only
   safe because step 1 makes the proxy the only possible sender.
4. Set `AllowedHosts` to the public host name.

nginx example (inside the `http` block):

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl;
    server_name servora.example.com;
    ssl_certificate     /etc/ssl/servora.crt;
    ssl_certificate_key /etc/ssl/servora.key;

    location / {
        proxy_pass         http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
    }
}
```

IIS: install *Application Request Routing* and *URL Rewrite*, enable the *WebSocket Protocol* feature, create a reverse
proxy rule to `http://127.0.0.1:5080` and enable "preserve host header"
(`appcmd set config -section:system.webServer/proxy -preserveHostHeader:true /commit:apphost`).

Windows single sign-on (Negotiate/Kerberos) through a reverse proxy requires constrained delegation and is not
recommended; use Kestrel HTTPS directly when SSO is needed.
