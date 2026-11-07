using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Notifications;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>
/// HTTP endpoints of the T-07 Platform Engine module: the Table REST API (<c>/api/v1/table/{table}</c>) for generic,
/// permission-checked record access (requires <c>api.access</c> plus the table permissions enforced by the record
/// service), record number resolution (<c>/api/v1/go/{number}</c>) and the current user's in-app notification inbox
/// (<c>/api/v1/notifications</c>). Errors are returned as RFC 7807 problem details; list sizes are bounded.
/// </summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PlatformEndpoints
{
    /// <summary>The maximum accepted request body size in bytes.</summary>
    public const int MaxBodyBytes = 256 * 1024;

    /// <summary>The maximum length of the <c>sysparm_query</c> parameter.</summary>
    public const int MaxQueryLength = 16_000;

    private static readonly JsonSerializerOptions s_conditionJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 32,
    };

    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/table")
            .RequireAuthorization(PermissionPolicy.For(Permissions.Api.Access))
            .WithTags("Table API");

        group.MapGet("/{table}", ListAsync)
            .WithName("TableList")
            .WithSummary("Queries records of a table (sysparm_query = condition JSON, sysparm_fields, sysparm_limit, sysparm_page / sysparm_offset, sysparm_order_by, sysparm_order_desc, sysparm_search, sysparm_display_value).");
        group.MapGet("/{table}/{id:guid}", GetAsync).WithName("TableGet").WithSummary("Gets a record.");
        group.MapPost("/{table}", CreateAsync).WithName("TableCreate").WithSummary("Creates a record from a JSON object of field values.");
        group.MapPatch("/{table}/{id:guid}", UpdateAsync).WithName("TableUpdate").WithSummary("Updates fields of a record from a JSON object.");
        group.MapDelete("/{table}/{id:guid}", DeleteAsync).WithName("TableDelete").WithSummary("Deletes a record.");

        app.MapGet("/api/v1/go/{number}", ResolveNumberAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Api.Access))
            .WithTags("Table API")
            .WithName("ResolveRecordNumber")
            .WithSummary("Resolves a record number (INC0001001, KB0001234, custom prefixes) to its table, identifier and UI route.");

        var inbox = app.MapGroup("/api/v1/notifications")
            .RequireAuthorization()
            .WithTags("Notifications");
        inbox.MapGet("/", InboxListAsync).WithName("InboxList").WithSummary("Lists the current user's in-app notifications (unread = true for unread only; page, pageSize ≤ 100).");
        inbox.MapGet("/unread-count", async (INotificationInbox notifications, CancellationToken ct) => Results.Ok(new { count = await notifications.GetUnreadCountAsync(ct) }))
            .WithName("InboxUnreadCount").WithSummary("Gets the number of unread notifications of the current user.");
        inbox.MapPost("/{id:guid}/read", async (Guid id, INotificationInbox notifications, CancellationToken ct) => ToResult(await notifications.MarkReadAsync(id, ct)))
            .WithName("InboxMarkRead").WithSummary("Marks a notification as read.");
        inbox.MapPost("/read-all", async (INotificationInbox notifications, CancellationToken ct) => ToResult(await notifications.MarkAllReadAsync(ct)))
            .WithName("InboxMarkAllRead").WithSummary("Marks all notifications of the current user as read.");
        inbox.MapDelete("/{id:guid}", async (Guid id, INotificationInbox notifications, CancellationToken ct) => ToResult(await notifications.DeleteAsync(id, ct)))
            .WithName("InboxDelete").WithSummary("Deletes a notification of the current user.");
        return app;
    }

    private static async Task<IResult> ListAsync(string table, HttpRequest request, IRecordService records, CancellationToken cancellationToken)
    {
        var query = request.Query;
        ConditionGroup? condition = null;
        var rawCondition = query["sysparm_query"].ToString();
        if (!string.IsNullOrWhiteSpace(rawCondition))
        {
            if (rawCondition.Length > MaxQueryLength)
            {
                return Problem(StatusCodes.Status400BadRequest, "Invalid query", $"sysparm_query must not exceed {MaxQueryLength} characters.");
            }

            try
            {
                condition = JsonSerializer.Deserialize<ConditionGroup>(rawCondition, s_conditionJson);
            }
            catch (JsonException)
            {
                return Problem(StatusCodes.Status400BadRequest, "Invalid query", "sysparm_query must be a condition JSON object ({\"logic\":\"And\",\"rules\":[{\"field\":\"…\",\"operator\":\"Equals\",\"value\":\"…\"}]}).");
            }
        }

        if (!TryReadInt(query["sysparm_limit"], 25, 1, PageRequest.MaxPageSize, out var limit)
            || !TryReadInt(query["sysparm_offset"], 0, 0, int.MaxValue, out var offset)
            || !TryReadInt(query["sysparm_page"], 0, 0, 1_000_000, out var page))
        {
            return Problem(StatusCodes.Status400BadRequest, "Invalid paging", $"sysparm_limit must be between 1 and {PageRequest.MaxPageSize}; sysparm_offset and sysparm_page must be positive numbers.");
        }

        var fields = query["sysparm_fields"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var result = await records.QueryAsync(table, new RecordQuery
        {
            Condition = condition,
            Fields = fields,
            IncludeDisplayValues = !string.Equals(query["sysparm_display_value"], "false", StringComparison.OrdinalIgnoreCase),
            Page = new PageRequest
            {
                PageSize = limit,
                Page = page > 0 ? page : (offset / limit) + 1,
                Search = NullIfEmpty(query["sysparm_search"]),
                SortBy = NullIfEmpty(query["sysparm_order_by"]),
                SortDescending = string.Equals(query["sysparm_order_desc"], "true", StringComparison.OrdinalIgnoreCase),
            },
        }, cancellationToken);
        if (!result.Succeeded)
        {
            return Problem(result);
        }

        request.HttpContext.Response.Headers["X-Total-Count"] = result.Value!.TotalCount.ToString(CultureInfo.InvariantCulture);
        return Results.Ok(new
        {
            result = result.Value.Items,
            total = result.Value.TotalCount,
            page = result.Value.Page,
            pageSize = result.Value.PageSize,
        });
    }

    private static async Task<IResult> GetAsync(string table, Guid id, IRecordService records, CancellationToken cancellationToken)
    {
        var result = await records.GetAsync(table, id, cancellationToken);
        return result.Succeeded ? Results.Ok(new { result = result.Value }) : Problem(result);
    }

    private static async Task<IResult> CreateAsync(string table, HttpRequest request, IRecordService records, CancellationToken cancellationToken)
    {
        var (values, error) = await ReadBodyAsync(request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var created = await records.CreateAsync(table, values!, cancellationToken);
        if (!created.Succeeded)
        {
            return Problem(created);
        }

        var record = await records.GetAsync(table, created.Value, cancellationToken);
        return Results.Created($"/api/v1/table/{Uri.EscapeDataString(table)}/{created.Value}", new { result = record.Succeeded ? record.Value : new Dictionary<string, object?> { ["Id"] = created.Value } });
    }

    private static async Task<IResult> UpdateAsync(string table, Guid id, HttpRequest request, IRecordService records, CancellationToken cancellationToken)
    {
        var (values, error) = await ReadBodyAsync(request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var updated = await records.UpdateAsync(table, id, values!, cancellationToken);
        if (!updated.Succeeded)
        {
            return Problem(updated);
        }

        var record = await records.GetAsync(table, id, cancellationToken);
        return record.Succeeded ? Results.Ok(new { result = record.Value }) : Problem(record);
    }

    private static async Task<IResult> DeleteAsync(string table, Guid id, IRecordService records, CancellationToken cancellationToken)
    {
        var result = await records.DeleteAsync(table, id, cancellationToken);
        return result.Succeeded ? Results.NoContent() : Problem(result);
    }

    private static async Task<IResult> ResolveNumberAsync(string number, IRecordNumberResolver resolver, CancellationToken cancellationToken)
    {
        if (number.Length > 40)
        {
            return Problem(StatusCodes.Status400BadRequest, "Invalid number", "Record numbers are at most 40 characters long.");
        }

        var result = await resolver.ResolveAsync(number, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new { result = new { table = result.Value!.TableName, id = result.Value.Id, url = result.Value.Url } })
            : Problem(result);
    }

    private static async Task<IResult> InboxListAsync(HttpRequest request, INotificationInbox notifications, CancellationToken cancellationToken)
    {
        var query = request.Query;
        if (!TryReadInt(query["page"], 1, 1, 1_000_000, out var page) || !TryReadInt(query["pageSize"], 20, 1, 100, out var pageSize))
        {
            return Problem(StatusCodes.Status400BadRequest, "Invalid paging", "page must be positive and pageSize between 1 and 100.");
        }

        var unreadOnly = string.Equals(query["unread"], "true", StringComparison.OrdinalIgnoreCase);
        var result = await notifications.GetAsync(new PageRequest { Page = page, PageSize = pageSize }, unreadOnly, cancellationToken);
        return Results.Ok(new { result = result.Items, total = result.TotalCount, page = result.Page, pageSize = result.PageSize });
    }

    private static IResult ToResult(Result result) => result.Succeeded ? Results.NoContent() : Problem(result);

    private static async Task<(Dictionary<string, object?>? Values, IResult? Error)> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        // JSON only: this also prevents cross-site form posts (which cannot send application/json without CORS).
        if (!request.HasJsonContentType())
        {
            return (null, Problem(StatusCodes.Status415UnsupportedMediaType, "Unsupported media type", "Send the field values as application/json."));
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            return (null, Problem(StatusCodes.Status413PayloadTooLarge, "Payload too large", $"The request body must not exceed {MaxBodyBytes / 1024} KB."));
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return (null, Problem(StatusCodes.Status413PayloadTooLarge, "Payload too large", $"The request body must not exceed {MaxBodyBytes / 1024} KB."));
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, Problem(StatusCodes.Status400BadRequest, "Invalid body", "The body must be a JSON object of field values."));
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.Clone();
            }

            return (values, null);
        }
        catch (JsonException)
        {
            return (null, Problem(StatusCodes.Status400BadRequest, "Invalid body", "The body is not valid JSON."));
        }
    }

    private static bool TryReadInt(string? text, int fallback, int min, int max, out int value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = fallback;
            return true;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static IResult Problem(Result result) => result.ErrorKind switch
    {
        ResultErrorKind.Validation => Problem(StatusCodes.Status400BadRequest, "Invalid request", result.Error),
        ResultErrorKind.NotFound => Problem(StatusCodes.Status404NotFound, "Not found", result.Error),
        ResultErrorKind.Forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden", result.Error),
        ResultErrorKind.Conflict => Problem(StatusCodes.Status409Conflict, "Conflict", result.Error),
        _ => Problem(StatusCodes.Status422UnprocessableEntity, "Request could not be processed", result.Error),
    };

    private static IResult Problem(int status, string title, string? detail) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}
