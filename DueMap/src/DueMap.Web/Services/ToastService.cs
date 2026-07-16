namespace DueMap.Web.Services;

public enum ToastLevel { Success, Info, Warning, Error }

public sealed record ToastItem(Guid Id, string Message, ToastLevel Level, int DurationMs);

/// <summary>
/// Circuit-scoped transient notifications. Pages report ACTION RESULTS here
/// ("saved", "synced 12 invoices", "link failed: …"); the ToastHost overlay —
/// rendered by every layout — shows them top-center floating above the topbar
/// and removes them automatically after a few seconds. Contextual alerts
/// (form validation, mapping warnings, empty states) stay inline in their
/// pages: an error the user must act on shouldn't vanish on its own.
/// </summary>
public sealed class ToastService
{
    private readonly object _gate = new();
    private readonly List<ToastItem> _items = new();

    /// <summary>Raised on every add/remove. ToastHost re-renders off this.</summary>
    public event Action? Changed;

    public IReadOnlyList<ToastItem> Items
    {
        get { lock (_gate) return _items.ToArray(); }
    }

    public void Success(string message, int durationMs = 4_000) => Add(message, ToastLevel.Success, durationMs);
    public void Info(string message, int durationMs = 4_000) => Add(message, ToastLevel.Info, durationMs);
    public void Warning(string message, int durationMs = 6_000) => Add(message, ToastLevel.Warning, durationMs);

    /// <summary>Errors linger longest — still self-dismissing, but readable.</summary>
    public void Error(string message, int durationMs = 8_000) => Add(message, ToastLevel.Error, durationMs);

    public void Remove(Guid id)
    {
        bool removed;
        lock (_gate) removed = _items.RemoveAll(t => t.Id == id) > 0;
        if (removed) Changed?.Invoke();
    }

    private void Add(string message, ToastLevel level, int durationMs)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        lock (_gate) _items.Add(new ToastItem(Guid.NewGuid(), message.Trim(), level, durationMs));
        Changed?.Invoke();
    }
}
