using CompanyDirectory.Shared.Configuration;
using CompanyDirectory_Desktop.Services;
using CompanyDirectory_Desktop.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using System.Reflection;
using System.Security.Principal;

namespace CompanyDirectory_Desktop.Activation;

public class AppActivationService(
    MainWindow mainWindow,
    AdminMessageWindow adminMessageWindow,
    SeedDataService seedService,
    TrayIconService trayIcon,
    GlobalHotkeyService hotkey,
    SyncBackgroundService syncBackground,
    ToastNotificationService toasts,
    SignalRService signalR,
    ChatService chatService,
    IApiClient apiClient,
    IOptions<UiSettings> uiOptions,
    ILogger<AppActivationService> logger)
{
    private readonly UiSettings _ui = uiOptions.Value;
    private int _unreadCount;
    private int _chatUnreadCount;

    public async Task ActivateAsync(LaunchActivatedEventArgs args)
    {
        logger.LogInformation("Application activating");

        toasts.Register(onActivated: arg =>
        {
            if (arg.Contains("action=inbox"))
                mainWindow.ShowAndNavigateTo("inbox");
            else if (arg.Contains("action=chat"))
                mainWindow.ShowAndNavigateTo("chat");
            else
                mainWindow.ShowAndActivate();
        });

        await seedService.SeedIfEmptyAsync();

        trayIcon.OpenRequested     += (_, _) => mainWindow.ShowAndActivate();
        trayIcon.InboxRequested    += (_, _) => mainWindow.ShowAndNavigateTo("inbox");
        trayIcon.SentRequested     += (_, _) => mainWindow.ShowAndNavigateTo("inbox");
        trayIcon.SmsRequested      += (_, _) => mainWindow.ShowAndNavigateTo("sms");
        trayIcon.ChatRequested     += (_, _) => mainWindow.ShowAndNavigateTo("chat");
        trayIcon.SettingsRequested += (_, _) => mainWindow.ShowAndNavigateTo("settings");
        trayIcon.AdminRequested    += (_, _) => adminMessageWindow.ShowAndActivate();
        trayIcon.ExitRequested     += (_, _) => Shutdown();
        trayIcon.Initialize();

        mainWindow.NavigatedToInbox += (_, _) =>
        {
            _unreadCount = 0;
            trayIcon.SetUnreadCount(0);
            mainWindow.ViewModel.UnreadCount = 0;
        };

        hotkey.HotkeyPressed += (_, _) => ToggleWindow();
        hotkey.Start();

        syncBackground.Start();

        signalR.MessageReceived += msg =>
        {
            _unreadCount++;
            trayIcon.SetUnreadCount(_unreadCount);
            mainWindow.ViewModel.UnreadCount = _unreadCount;
            var preview = msg.Body.Length > 120 ? msg.Body[..120] + "…" : msg.Body;
            toasts.ShowNewMessage(msg.Title, preview);
            mainWindow.EnqueueMessage(msg);
        };
        signalR.UnreadMessagesReceived += msgs =>
        {
            _unreadCount += msgs.Count;
            trayIcon.SetUnreadCount(_unreadCount);
            mainWindow.ViewModel.UnreadCount = _unreadCount;
            foreach (var m in msgs) mainWindow.EnqueueMessage(m);
        };
        // Resolve own login to filter out echoed chat messages
        var myLogin = string.Empty;
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var name = identity.Name ?? string.Empty;
            myLogin = name.Contains('\\') ? name.Split('\\')[1].ToLowerInvariant() : name.ToLowerInvariant();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve current user login");
        }

        chatService.MessageReceived += msg =>
        {
            if (msg.FromLogin.Equals(myLogin, StringComparison.OrdinalIgnoreCase)) return;

            var preview = msg.Body.Length > 100 ? msg.Body[..100] + "…" : msg.Body;
            toasts.ShowNewChatMessage(msg.FromDisplayName, preview);

            App.DispatcherQueue.TryEnqueue(() =>
            {
                _chatUnreadCount++;
                mainWindow.ViewModel.ChatUnreadCount = _chatUnreadCount;
            });
        };

        mainWindow.NavigatedToChat += (_, _) =>
        {
            _chatUnreadCount = 0;
            App.DispatcherQueue.TryEnqueue(() =>
                mainWindow.ViewModel.ChatUnreadCount = 0);
        };

        _ = signalR.StartAsync();
        _ = chatService.StartAsync();
        _ = CheckForUpdatesAsync();

        mainWindow.CloseToTray = _ui.CloseToTray;

        // AdminMessageWindow must be activated once before it can be shown via AppWindow.Show()
        adminMessageWindow.Activate();
        adminMessageWindow.HideWindow();

        if (_ui.StartMinimizedToTray)
        {
            logger.LogInformation("Starting minimized to tray");
            mainWindow.Activate();
            mainWindow.HideWindow();
        }
        else
        {
            mainWindow.ShowAndActivate();
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var info = await apiClient.GetVersionInfoAsync();
            if (info is null || string.IsNullOrEmpty(info.Version)) return;

            var remote = Version.Parse(info.Version);
            var local  = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            if (remote > local)
            {
                logger.LogInformation("Update available: {Remote} (local: {Local})", remote, local);
                mainWindow.ShowUpdateBar(info);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update check failed");
        }
    }

    private void ToggleWindow()
    {
        if (mainWindow.IsVisible)
            mainWindow.HideWindow();
        else
            mainWindow.ShowAndActivate();
    }

    private void Shutdown()
    {
        logger.LogInformation("Shutting down application");
        syncBackground.Dispose();
        hotkey.Dispose();
        trayIcon.Dispose();
        toasts.Dispose();
        _ = signalR.StopAsync();
        _ = chatService.StopAsync();
        Application.Current.Exit();
    }
}
