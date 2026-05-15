using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CompanyDirectory_Desktop.Services;

public sealed class ToastNotificationService : IDisposable
{
    private readonly ILogger<ToastNotificationService> _logger;
    private bool _registered;
    private bool _disposed;

    public ToastNotificationService(ILogger<ToastNotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Must be called once at startup.
    /// onActivated receives the notification argument string (e.g. "action=inbox").
    /// </summary>
    public void Register(Action<string>? onActivated = null)
    {
        if (_registered) return;
        try
        {
            if (onActivated is not null)
                AppNotificationManager.Default.NotificationInvoked += (_, e) =>
                    App.DispatcherQueue.TryEnqueue(() => onActivated(e.Argument));

            AppNotificationManager.Default.Register();
            _registered = true;
            _logger.LogInformation("Toast notifications registered");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Toast notifications unavailable — continuing without them");
        }
    }

    public void ShowNewMessage(string title, string preview)
    {
        Show(new AppNotificationBuilder()
            .AddText(title)
            .AddText(preview)
            .AddButton(new AppNotificationButton("Otwórz skrzynkę")
                .AddArgument("action", "inbox")));
    }

    public void ShowNewChatMessage(string fromDisplayName, string preview)
    {
        Show(new AppNotificationBuilder()
            .AddText($"Nowa wiadomość od {fromDisplayName}")
            .AddText(preview)
            .AddButton(new AppNotificationButton("Otwórz czat")
                .AddArgument("action", "chat")));
    }

    public void ShowSyncFailed()
    {
        Show(new AppNotificationBuilder()
            .AddText("CompanyDirectory — synchronizacja nieudana")
            .AddText("Nie można pobrać danych z API. Wyniki mogą być nieaktualne."));
    }

    public void ShowHotkeyConflict(string modifiers, string key)
    {
        Show(new AppNotificationBuilder()
            .AddText("CompanyDirectory — skrót niedostępny")
            .AddText($"{modifiers}+{key} jest zajęty przez inną aplikację. Zmień go w Ustawieniach."));
    }

    private void Show(AppNotificationBuilder builder)
    {
        if (!_registered)
        {
            _logger.LogWarning("Toast skipped — AppNotificationManager not registered (packaged MSIX required)");
            return;
        }
        try
        {
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered)
        {
            try { AppNotificationManager.Default.Unregister(); }
            catch { /* ignore on shutdown */ }
            _registered = false;
        }
    }
}
