using System.Globalization;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Changes;

namespace Servora.Web.Components.ServiceManagement;

/// <summary>Display labels, pill tones and icons for the service management enums.</summary>
public static class SmLabels
{
    /// <summary>Gets the label of a change state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(ChangeState state) => state.ToString();

    /// <summary>Gets the label of a change type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Label(ChangeType type) => type.ToString();

    /// <summary>Gets the label of a risk level.</summary>
    /// <param name="risk">The risk.</param>
    /// <returns>The label.</returns>
    public static string Label(ChangeRisk risk) => risk == ChangeRisk.VeryHigh ? "Very high" : risk.ToString();

    /// <summary>Gets the label of a close code.</summary>
    /// <param name="code">The close code.</param>
    /// <returns>The label.</returns>
    public static string Label(ChangeCloseCode code) => code switch
    {
        ChangeCloseCode.SuccessfulWithIssues => "Successful with issues",
        _ => code.ToString(),
    };

    /// <summary>Gets the label of a change task state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(ChangeTaskState state) => state == ChangeTaskState.InProgress ? "In progress" : state.ToString();

    /// <summary>Gets the label of an approval status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label.</returns>
    public static string Label(ApprovalStatus status) => status switch
    {
        ApprovalStatus.NotRequested => "Not requested",
        ApprovalStatus.Requested => "Requested",
        _ => status.ToString(),
    };

    /// <summary>Gets the label of an approval row state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(ApprovalRecordState state) => state switch
    {
        ApprovalRecordState.Requested => "Pending",
        ApprovalRecordState.NotRequired => "No longer required",
        _ => state.ToString(),
    };

    /// <summary>Gets the label of a request state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(RequestState state) => state switch
    {
        RequestState.Submitted => "Awaiting approval",
        RequestState.InProgress => "In progress",
        _ => state.ToString(),
    };

    /// <summary>Gets the label of a requested item state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(RequestItemState state) => state switch
    {
        RequestItemState.PendingApproval => "Awaiting approval",
        RequestItemState.InFulfillment => "In fulfilment",
        _ => state.ToString(),
    };

    /// <summary>Gets the label of a catalog task state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(CatalogTaskState state) => state == CatalogTaskState.WorkInProgress ? "Work in progress" : state.ToString();

    /// <summary>Gets the label of a variable type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Label(CatalogVariableType type) => type switch
    {
        CatalogVariableType.MultilineText => "Multi-line text",
        CatalogVariableType.Boolean => "Yes / No",
        CatalogVariableType.UserReference => "User",
        _ => type.ToString(),
    };

    /// <summary>Gets the icon of a variable type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The icon name.</returns>
    public static string Icon(CatalogVariableType type) => type switch
    {
        CatalogVariableType.MultilineText => "file-text",
        CatalogVariableType.Number => "gauge",
        CatalogVariableType.Choice => "list",
        CatalogVariableType.Boolean => "check-square",
        CatalogVariableType.Date => "calendar",
        CatalogVariableType.UserReference => "user",
        _ => "edit",
    };

    /// <summary>Gets the pill tone of a change state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ChangeState state) => state switch
    {
        ChangeState.New => "info",
        ChangeState.Assess => "purple",
        ChangeState.Authorize => "warning",
        ChangeState.Scheduled => "accent",
        ChangeState.Implement => "primary",
        ChangeState.Review => "primary",
        ChangeState.Closed => "success",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of a change type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ChangeType type) => type switch
    {
        ChangeType.Emergency => "danger",
        ChangeType.Standard => "success",
        _ => "info",
    };

    /// <summary>Gets the pill tone of a risk level.</summary>
    /// <param name="risk">The risk.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ChangeRisk risk) => risk switch
    {
        ChangeRisk.Low => "success",
        ChangeRisk.Moderate => "warning",
        _ => "danger",
    };

    /// <summary>Gets the pill tone of an approval status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Requested => "warning",
        ApprovalStatus.Approved => "success",
        ApprovalStatus.Rejected => "danger",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of an approval row state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ApprovalRecordState state) => state switch
    {
        ApprovalRecordState.Requested => "warning",
        ApprovalRecordState.Approved => "success",
        ApprovalRecordState.Rejected => "danger",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of a request state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(RequestState state) => state switch
    {
        RequestState.Submitted => "warning",
        RequestState.InProgress => "primary",
        RequestState.Completed => "success",
        RequestState.Rejected => "danger",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of a requested item state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(RequestItemState state) => state switch
    {
        RequestItemState.PendingApproval => "warning",
        RequestItemState.InFulfillment => "primary",
        RequestItemState.Completed => "success",
        RequestItemState.Rejected => "danger",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of a catalog task state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(CatalogTaskState state) => state switch
    {
        CatalogTaskState.Open => "info",
        CatalogTaskState.WorkInProgress => "primary",
        CatalogTaskState.Completed => "success",
        _ => "neutral",
    };

    /// <summary>Gets the pill tone of a change task state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string Tone(ChangeTaskState state) => state switch
    {
        ChangeTaskState.Open => "info",
        ChangeTaskState.InProgress => "primary",
        ChangeTaskState.Completed => "success",
        _ => "neutral",
    };

    /// <summary>Formats a UTC timestamp for planning views.</summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The text, for example <c>Sat 14 Nov 22:00</c>.</returns>
    public static string Utc(DateTimeOffset? value) =>
        value is { } v ? v.ToUniversalTime().ToString("ddd d MMM yyyy HH:mm", CultureInfo.InvariantCulture) : "—";

    /// <summary>Formats a price.</summary>
    /// <param name="price">The price.</param>
    /// <returns>The text ("Free" for zero).</returns>
    public static string Price(decimal price) => price == 0 ? "Free" : price.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
