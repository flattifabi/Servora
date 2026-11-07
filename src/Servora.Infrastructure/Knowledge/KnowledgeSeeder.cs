using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Knowledge;

/// <summary>
/// Seeds two knowledge bases with categories (idempotent by name) and – only when no article exists – eight demo articles in
/// different workflow states, linked to configuration items of the demo service map.
/// </summary>
/// <param name="numbers">The number sequence service.</param>
public sealed class KnowledgeSeeder(INumberSequenceService numbers) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 130;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var it = await EnsureBaseAsync(db, "IT Knowledge", "How-tos, troubleshooting guides and runbooks for IT services.", "book-open", 10, cancellationToken);
        var hr = await EnsureBaseAsync(db, "HR & Workplace", "Policies, onboarding and everything about working here.", "users", 20, cancellationToken);
        var categories = new Dictionary<string, KnowledgeCategory>(StringComparer.Ordinal)
        {
            ["start"] = await EnsureCategoryAsync(db, it, "Getting Started", "First steps with your devices and accounts.", "rocket", 10, cancellationToken),
            ["access"] = await EnsureCategoryAsync(db, it, "Accounts & Access", "Passwords, MFA, permissions and single sign-on.", "key", 20, cancellationToken),
            ["mail"] = await EnsureCategoryAsync(db, it, "Email & Collaboration", "Outlook, calendars, Teams and shared mailboxes.", "mail", 30, cancellationToken),
            ["network"] = await EnsureCategoryAsync(db, it, "Network & VPN", "Remote access, Wi-Fi and connectivity.", "globe", 40, cancellationToken),
            ["hardware"] = await EnsureCategoryAsync(db, it, "Hardware & Devices", "Laptops, monitors, phones and peripherals.", "laptop", 50, cancellationToken),
            ["apps"] = await EnsureCategoryAsync(db, it, "Business Applications", "Runbooks and guides for business applications.", "grid", 60, cancellationToken),
            ["onboarding"] = await EnsureCategoryAsync(db, hr, "Onboarding", "Your first days and weeks.", "user-plus", 10, cancellationToken),
            ["policies"] = await EnsureCategoryAsync(db, hr, "Policies", "Company policies and guidelines.", "file-text", 20, cancellationToken),
        };
        await db.SaveChangesAsync(cancellationToken);

        if (await db.Set<KnowledgeArticle>().AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await db.Set<User>().Where(u => u.NormalizedUserName == "ADMIN").Select(u => new { u.Id, u.DisplayName }).FirstOrDefaultAsync(cancellationToken);
        var cis = await db.Set<ConfigurationItem>().Select(c => new { c.Id, c.Name }).Take(500).ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        async Task AddAsync(string title, string summary, string category, KnowledgeArticleState state, int daysAgo, int views, int helpful, int notHelpful, string keywords, string[] ciNames, string body)
        {
            var cat = categories[category];
            var article = new KnowledgeArticle
            {
                Number = await numbers.NextAsync(TableNames.KnowledgeArticle, "KB", cancellationToken),
                Title = title,
                Summary = summary,
                Body = body.Trim(),
                KnowledgeBaseId = cat.KnowledgeBaseId,
                CategoryId = cat.Id,
                State = state,
                AuthorId = admin?.Id,
                AuthorName = admin?.DisplayName ?? "System Administrator",
                PublishedAt = state == KnowledgeArticleState.Published ? now.AddDays(-daysAgo) : null,
                ViewCount = views,
                HelpfulCount = helpful,
                NotHelpfulCount = notHelpful,
                Keywords = keywords,
                ArticleVersion = 1,
                CreatedAt = now.AddDays(-daysAgo - 2),
            };
            db.Set<KnowledgeArticle>().Add(article);
            db.Set<KnowledgeArticleVersion>().Add(new KnowledgeArticleVersion
            {
                ArticleId = article.Id,
                VersionNumber = 1,
                Title = article.Title,
                Summary = article.Summary,
                Body = article.Body,
                State = state,
                ChangeNote = "Initial version",
                CreatedBy = article.AuthorName,
                CreatedAt = article.CreatedAt,
            });
            foreach (var name in ciNames.Where(cis.ContainsKey))
            {
                db.Set<KnowledgeArticleCi>().Add(new KnowledgeArticleCi { ArticleId = article.Id, ConfigurationItemId = cis[name] });
            }
        }

        await AddAsync("How to reset your password", "Reset a forgotten or expired password yourself in under two minutes using the self-service portal.", "access",
            KnowledgeArticleState.Published, 120, 1_284, 168, 9, "password, reset, forgot, expired, locked, account, login", ["Identity Provider (SSO)"], ResetPassword);
        await AddAsync("Connecting to the corporate VPN", "Install the VPN client, sign in with MFA and fix the most common connection problems.", "network",
            KnowledgeArticleState.Published, 95, 972, 121, 14, "vpn, remote, home office, globalprotect, tunnel, connection", ["fw-edge-01"], Vpn);
        await AddAsync("Setting up multi-factor authentication (MFA)", "Register the authenticator app as second factor and keep access when you change phones.", "access",
            KnowledgeArticleState.Published, 80, 811, 96, 6, "mfa, 2fa, authenticator, otp, second factor, security", ["Identity Provider (SSO)"], Mfa);
        await AddAsync("Configuring Outlook on a new laptop", "Add your mailbox, shared mailboxes and signature to Outlook in a few steps.", "mail",
            KnowledgeArticleState.Published, 60, 534, 61, 7, "outlook, email, mailbox, shared mailbox, signature, calendar", ["Mail Transport Service", "Corporate Email"], Outlook);
        await AddAsync("Online Shop outage runbook", "Step-by-step diagnosis and recovery procedure for the Online Shop business service (P1).", "apps",
            KnowledgeArticleState.Published, 30, 214, 27, 1, "shop, outage, runbook, p1, checkout, order api, database, failover", ["Online Shop", "Order Service API", "shop-db-prd", "lb-prd-01"], ShopRunbook);
        await AddAsync("New employee onboarding checklist", "Everything new colleagues need in their first week – accounts, equipment, trainings.", "onboarding",
            KnowledgeArticleState.Published, 45, 389, 44, 2, "onboarding, new hire, first day, checklist, equipment", [], Onboarding);
        await AddAsync("Troubleshooting slow CRM performance", "Checks for slow CRM screens, from client cache to database blocking.", "apps",
            KnowledgeArticleState.Review, 3, 0, 0, 0, "crm, slow, performance, latency, sql, blocking", ["CRM Application", "crm-db-prd"], CrmPerformance);
        await AddAsync("Requesting new hardware", "Which devices are standard, how to order them and how long delivery takes.", "hardware",
            KnowledgeArticleState.Draft, 1, 0, 0, 0, "hardware, laptop, monitor, order, request, catalog", [], Hardware);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<KnowledgeBase> EnsureBaseAsync(IAppDbContext db, string name, string description, string icon, int order, CancellationToken cancellationToken)
    {
        var existing = await db.Set<KnowledgeBase>().FirstOrDefaultAsync(b => b.Name == name, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var knowledgeBase = new KnowledgeBase { Name = name, Description = description, Icon = icon, SortOrder = order };
        db.Set<KnowledgeBase>().Add(knowledgeBase);
        return knowledgeBase;
    }

    private static async Task<KnowledgeCategory> EnsureCategoryAsync(IAppDbContext db, KnowledgeBase knowledgeBase, string name, string description, string icon, int order, CancellationToken cancellationToken)
    {
        var existing = await db.Set<KnowledgeCategory>().FirstOrDefaultAsync(c => c.KnowledgeBaseId == knowledgeBase.Id && c.Name == name, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var category = new KnowledgeCategory { KnowledgeBaseId = knowledgeBase.Id, Name = name, Description = description, Icon = icon, SortOrder = order };
        db.Set<KnowledgeCategory>().Add(category);
        return category;
    }

    private const string ResetPassword = """
        Forgot your password or did it expire? You can reset it yourself – no ticket needed.

        ## Before you start

        - You need access to your **registered MFA device** (authenticator app or phone).
        - Passwords must have at least **12 characters** and must not contain your user name.
        - The last 5 passwords cannot be reused.

        ## Reset your password

        1. Open the sign-in page and select **Forgot password?**
        2. Enter your user name, for example `j.smith`.
        3. Confirm the push notification or enter the code from your authenticator app.
        4. Choose a new password and confirm it.
        5. Sign in again on all devices (laptop, phone, VPN client).

        > **Tip:** Use a passphrase such as `Coffee-Mountain-Bicycle-42` – long, memorable and hard to guess.

        ## Account locked?

        After **5 failed attempts** your account is locked for 15 minutes. Wait and try again, or contact the Service Desk if the problem persists.

        | Symptom | What to do |
        |---|---|
        | "Account locked" | Wait 15 minutes, then reset the password |
        | No MFA prompt arrives | Check the phone's internet connection, then use a one-time code |
        | Password rejected | Check the [password policy](/knowledge) – length and history rules |

        ## Still stuck?

        Contact the Service Desk via the portal or call extension **4357** (HELP).
        """;

    private const string Vpn = """
        The corporate VPN gives you secure access to internal applications from home or while travelling.

        ## Install the client

        1. Open the **Software Center** on your laptop.
        2. Install **GlobalProtect VPN** (it is pre-installed on new devices).
        3. Restart the laptop when prompted.

        ## Connect

        1. Click the globe icon in the system tray.
        2. Enter the portal address `vpn.example.com`.
        3. Sign in with your corporate account and confirm the MFA prompt.

        The status changes to **Connected** after a few seconds.

        ## Troubleshooting

        ### "Gateway not reachable"

        - Check that you have internet access (open any public website).
        - Hotel and guest Wi-Fi networks sometimes block VPN traffic – try a mobile hotspot.

        ### Connection drops every few minutes

        Disable *power saving* for the Wi-Fi adapter:

        ```powershell
        powercfg /setacvalueindex SCHEME_CURRENT SUB_NONE CONNECTIVITYINSTANDBY 1
        ```

        ### Internal sites do not load

        Flush the DNS cache and reconnect:

        ```powershell
        ipconfig /flushdns
        ```

        If none of this helps, open an incident and attach the client logs (*Settings → Troubleshooting → Collect logs*).
        """;

    private const string Mfa = """
        Multi-factor authentication (MFA) protects your account even if your password is stolen. It is **mandatory** for all employees.

        ## Register the authenticator app

        1. Install the authenticator app from the App Store or Google Play.
        2. Sign in to `https://sso.example.com/account` on your laptop.
        3. Select **Security → Add sign-in method → Authenticator app**.
        4. Scan the QR code with the app and enter the 6-digit code to confirm.

        ## Recommended setup

        - Register **two methods** (app and phone number) so you never lock yourself out.
        - Enable *cloud backup* in the authenticator app.

        ## Changing your phone

        Before you reset or hand in your old phone:

        1. Add the new phone as an additional method.
        2. Test a sign-in with the new phone.
        3. Remove the old phone from your security info.

        > Lost your phone? Contact the Service Desk immediately – they will revoke the old device and issue a temporary access pass.
        """;

    private const string Outlook = """
        Outlook configures itself automatically for your own mailbox. This article covers the extras.

        ## First start

        1. Open **Outlook** and confirm your e-mail address.
        2. Sign in with your corporate account (MFA may be required).
        3. Wait until the mailbox is synchronised – this can take a few minutes for large mailboxes.

        ## Add a shared mailbox

        Shared mailboxes you have access to appear automatically after up to 60 minutes. To add one manually:

        1. *File → Account settings → Account settings…*
        2. Select your account → **Change → More settings → Advanced**.
        3. Add the mailbox, for example `servicedesk@example.com`.

        ## Signature

        Use the corporate template from the intranet and keep it short:

        ```
        Jane Smith
        Senior Analyst · Finance
        +49 30 1234 567
        ```

        ## Known issues

        | Issue | Fix |
        |---|---|
        | Calendar of a colleague not visible | Ask them to share it with **Reviewer** permissions |
        | Search returns no results | *File → Options → Search → Indexing options → Rebuild* |
        """;

    private const string ShopRunbook = """
        Runbook for **P1 incidents** affecting the *Online Shop* business service. Follow the steps in order and document every action as a work note.

        ## Service overview

        | Component | CI | Owner |
        |---|---|---|
        | Storefront | Shop Web Frontend on `web-prd-01/02` | Digital Sales |
        | Order API | Order Service API on `app-prd-01/02` | Platform team |
        | Database | `shop-db-prd` (PostgreSQL, primary + replica) | Database Administration |
        | Load balancer | `lb-prd-01` | Network Operations |

        See the dependency graph of the **Online Shop** CI in the CMDB for the full picture.

        ## 1. Confirm the impact

        1. Open the shop homepage from outside the corporate network.
        2. Check the synthetic monitoring dashboard for failed checkout transactions.
        3. Declare a **major incident** if checkout is unavailable for more than 5 minutes.

        ## 2. Check the load balancer

        - Pool members `web-prd-01` and `web-prd-02` must be *green*.
        - A node in **maintenance** must be disabled in the pool.

        ## 3. Check the Order API

        ```bash
        curl -s https://orders.internal.example.com/health | jq .status
        ```

        Expected result: `"Healthy"`. If the API reports `Degraded`, look at the database connection pool metrics.

        ## 4. Check the database

        ```sql
        SELECT state, count(*) FROM pg_stat_activity GROUP BY state;
        ```

        - More than 400 *active* sessions indicates blocking – escalate to the DBA on call.
        - If the primary is down, initiate the **documented failover** (requires change approval in an emergency CAB).

        ## 5. Communicate

        - Update the status page every **30 minutes**.
        - Inform the business owner of *Digital Sales*.

        ## 6. After recovery

        1. Verify a test order end-to-end.
        2. Create a **problem record** for root cause analysis.
        3. Link this article to the incident.

        ~~Restart all nodes at once~~ – never restart both web nodes simultaneously.
        """;

    private const string Onboarding = """
        Welcome aboard! This checklist helps you (and your manager) get everything ready in your first week.

        ## Before day one (manager)

        - [ ] Order the standard laptop and monitor via the **Service Catalog**
        - [ ] Request access to team mailboxes and shared drives
        - [ ] Assign an onboarding buddy

        ## Day one

        1. Collect your laptop and badge at the IT counter (*Floor 1*).
        2. Sign in and **change your initial password**.
        3. Set up MFA – see *Setting up multi-factor authentication (MFA)*.
        4. Join the welcome session at 10:00.

        ## First week

        - Complete the mandatory **security awareness** training.
        - Review the code of conduct and data protection policy.
        - Book a meeting with your buddy and your manager.

        ## Useful contacts

        | Topic | Contact |
        |---|---|
        | IT problems | Service Desk – extension 4357 |
        | Payroll | payroll@example.com |
        | Facilities | facilities@example.com |
        """;

    private const string CrmPerformance = """
        Use this guide when users report that CRM screens take longer than **3 seconds** to load.

        ## Quick checks

        1. Is the problem limited to **one user**? Clear the browser cache and retry.
        2. Does it affect **all users**? Continue with the server checks.

        ## Server checks

        - CPU and memory on `crm-app-01` (threshold: 85 %).
        - Application pool recycling events in the Windows event log.

        ## Database checks

        Look for blocking sessions on `crm-db-prd`:

        ```sql
        SELECT session_id, blocking_session_id, wait_type, wait_time
        FROM sys.dm_exec_requests
        WHERE blocking_session_id <> 0;
        ```

        > Do **not** kill sessions of the nightly import job without consulting the DBA team.

        ## Escalation

        Escalate to *Database Administration* if blocking lasts longer than 10 minutes.
        """;

    private const string Hardware = """
        Standard hardware is ordered through the Service Catalog. Non-standard requests need manager approval.

        ## Standard devices

        | Role | Laptop | Monitor |
        |---|---|---|
        | Office | Dell Latitude 7440 | Dell UltraSharp 24 |
        | Developer | MacBook Pro 14 or ThinkPad X1 Carbon | 2× Dell UltraSharp 27 4K |
        | Field staff | ThinkPad T14 + docking station | – |

        ## Delivery times

        - Items **in stock**: 2 business days
        - Items to be ordered: 5–10 business days

        *Draft – delivery times to be confirmed with procurement.*
        """;
}
