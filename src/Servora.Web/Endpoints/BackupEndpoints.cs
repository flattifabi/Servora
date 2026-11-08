using Servora.Application.Backup;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>
/// HTTP endpoints of the T-15 Backup &amp; Restore module: archive download (permission <c>backup.manage</c>, checked again
/// by <see cref="IBackupService"/>) and the self-contained "restoring" page that waits until Servora answers on
/// <c>/health</c> again after the restart. Uploads go through the Blazor page (<c>InputFile</c>) and
/// <see cref="IBackupService.ImportAsync"/>, which enforces the size limit and validates the archive.
/// </summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class BackupEndpoints
{
    /// <summary>The route of the restoring page.</summary>
    public const string RestoringPath = "/admin/backups/restoring";

    private const string RestoringHtml =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Restoring · Servora</title>
        <link rel="icon" href="/favicon.svg" />
        <style>
          :root { color-scheme: light; }
          * { box-sizing: border-box; }
          body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
                 color: #15211b; background: #eef5f0; }
          .card { width: min(560px, calc(100vw - 32px)); padding: 36px 36px 30px; border-radius: 14px; background: #ffffff;
                  border: 1px solid #dfe7e2; }
          .ring { width: 56px; height: 56px; border-radius: 50%; margin-bottom: 22px; border: 3px solid #e3ede7;
                  border-top-color: #1b4535; animation: spin 1.1s linear infinite; }
          .done .ring { animation: none; border-color: #1e7a4c; }
          h1 { margin: 0 0 6px; font-size: 22px; font-weight: 700; letter-spacing: -.02em; }
          p { margin: 0; color: #56625c; line-height: 1.55; font-size: 14.5px; }
          ol { list-style: none; padding: 0; margin: 26px 0 0; display: grid; gap: 12px; }
          li { display: flex; align-items: center; gap: 12px; font-size: 14px; color: #65716b; }
          li .dot { width: 10px; height: 10px; border-radius: 50%; background: #cbd6d0; flex: none; }
          li.active { color: #15211b; font-weight: 600; } li.active .dot { background: #1b4535; box-shadow: 0 0 0 4px #e3ede7; }
          li.ok { color: #1e7a4c; } li.ok .dot { background: #1e7a4c; }
          .hint { margin-top: 24px; padding: 12px 14px; border-radius: 10px; background: #fcf0d9; border: 1px solid #f0d9a8;
                  color: #7a4800; font-size: 13.5px; display: none; }
          .meta { margin-top: 22px; font-size: 12px; color: #65716b; font-family: ui-monospace, Consolas, monospace; }
          @keyframes spin { to { transform: rotate(360deg); } }
        </style>
        </head>
        <body>
        <main class="card" id="card" aria-live="polite">
          <div class="ring" aria-hidden="true"></div>
          <h1 id="title">Restoring Servora</h1>
          <p id="text">Servora is restarting to restore the selected backup. This page reloads automatically when Servora is available again.</p>
          <ol>
            <li id="s1" class="active"><span class="dot"></span>Safety backup created, restore scheduled</li>
            <li id="s2"><span class="dot"></span>Servora stopped – restoring the database and files</li>
            <li id="s3"><span class="dot"></span>Migrating to the current version and starting</li>
          </ol>
          <div class="hint" id="hint">Servora is still running. If it runs in a console (development), stop it and start it again to perform the restore.</div>
          <div class="meta" id="meta">Waiting for the restart…</div>
        </main>
        <script>
          (function () {
            var sawDown = false, started = Date.now(), auto = new URLSearchParams(location.search).get("auto") === "1";
            function step(n) { for (var i = 1; i <= 3; i++) { var el = document.getElementById("s" + i); el.className = i < n ? "ok" : (i === n ? "active" : ""); } }
            function meta(t) { document.getElementById("meta").textContent = t; }
            function done() {
              step(4); document.getElementById("card").className = "card done";
              document.getElementById("title").textContent = "Servora is back";
              document.getElementById("text").textContent = "The restore has finished. Redirecting to Backup & Restore…";
              setTimeout(function () { location.href = "/admin/backups"; }, 1500);
            }
            function poll() {
              fetch("/health", { cache: "no-store", credentials: "same-origin" }).then(function (r) {
                if (r.ok && sawDown) { done(); return; }
                if (!r.ok) { sawDown = true; step(3); }
                schedule();
              }).catch(function () { if (!sawDown) { sawDown = true; step(2); } schedule(); });
            }
            function schedule() {
              var seconds = Math.round((Date.now() - started) / 1000);
              meta((sawDown ? "Servora is offline – waiting for it to come back" : "Waiting for the restart") + " · " + seconds + " s");
              if (!sawDown && seconds > 30 && !auto) { document.getElementById("hint").style.display = "block"; }
              setTimeout(poll, 2000);
            }
            setTimeout(poll, 1500);
          })();
        </script>
        </body>
        </html>
        """;

    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/backups/{id}/download", DownloadAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Backup.Manage))
            .ExcludeFromDescription();

        app.MapGet(RestoringPath, () => Results.Content(RestoringHtml, "text/html; charset=utf-8"))
            .RequireAuthorization(PermissionPolicy.For(Permissions.Backup.Read))
            .ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> DownloadAsync(string id, IBackupService backups, CancellationToken cancellationToken)
    {
        var result = await backups.OpenDownloadAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorKind switch
            {
                ResultErrorKind.Forbidden => Results.Forbid(),
                ResultErrorKind.NotFound => Results.NotFound(),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        var download = result.Value!;
        return Results.File(download.Content, "application/octet-stream", download.FileName, enableRangeProcessing: true);
    }
}
