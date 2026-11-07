namespace Servora.Web.Services;

/// <summary>Circuit-scoped toast notification service consumed by the <c>ToastHost</c> component.</summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _messages = [];

    /// <summary>Raised when the list of visible toasts changed.</summary>
    public event Action? Changed;

    /// <summary>Gets the visible toasts.</summary>
    public IReadOnlyList<ToastMessage> Messages => _messages;

    /// <summary>Shows a success toast.</summary>
    /// <param name="title">The title.</param>
    /// <param name="text">Optional detail text.</param>
    public void Success(string title, string? text = null) => Show(ToastLevel.Success, title, text);

    /// <summary>Shows an error toast.</summary>
    /// <param name="title">The title.</param>
    /// <param name="text">Optional detail text.</param>
    public void Error(string title, string? text = null) => Show(ToastLevel.Error, title, text);

    /// <summary>Shows a warning toast.</summary>
    /// <param name="title">The title.</param>
    /// <param name="text">Optional detail text.</param>
    public void Warning(string title, string? text = null) => Show(ToastLevel.Warning, title, text);

    /// <summary>Shows an information toast.</summary>
    /// <param name="title">The title.</param>
    /// <param name="text">Optional detail text.</param>
    public void Info(string title, string? text = null) => Show(ToastLevel.Info, title, text);

    /// <summary>Shows the outcome of an application result: a success toast or an error toast with the result's message.</summary>
    /// <param name="result">The result.</param>
    /// <param name="successTitle">The success title.</param>
    /// <returns><see langword="true"/> when the result succeeded.</returns>
    public bool FromResult(Servora.Application.Common.Result result, string successTitle)
    {
        if (result.Succeeded)
        {
            Success(successTitle);
            return true;
        }

        Error("Action failed", result.Error);
        return false;
    }

    /// <summary>Removes a toast.</summary>
    /// <param name="id">The toast identifier.</param>
    public void Dismiss(Guid id)
    {
        if (_messages.RemoveAll(m => m.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private void Show(ToastLevel level, string title, string? text)
    {
        var message = new ToastMessage(Guid.NewGuid(), level, title, text);
        _messages.Add(message);
        if (_messages.Count > 5)
        {
            _messages.RemoveAt(0);
        }

        Changed?.Invoke();
        _ = DismissLaterAsync(message.Id, level == ToastLevel.Error ? 8000 : 4500);
    }

    private async Task DismissLaterAsync(Guid id, int delay)
    {
        await Task.Delay(delay);
        Dismiss(id);
    }
}
