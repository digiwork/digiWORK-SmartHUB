using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.Services;
using CompanyDirectory_Desktop.ViewModels;
using CompanyDirectory_Desktop.Views.Pages;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;

namespace CompanyDirectory_Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public bool CloseToTray { get; set; }

    public event EventHandler? NavigatedToInbox;
    public event EventHandler? NavigatedToChat;

    private readonly UserSettingsService _settingsService;
    private readonly SignalRService _signalR;

    private readonly Queue<MessageDto> _messageQueue = new();
    private bool _showingMessage;
    private int _currentMessageId = -1;
    private Button? _confirmButton;

    private static readonly Dictionary<string, Type> _pageMap = new()
    {
        ["search"]   = typeof(SearchPage),
        ["org"]      = typeof(OrgChartPage),
        ["fav"]      = typeof(FavoritesPage),
        ["recent"]   = typeof(RecentPage),
        ["chat"]     = typeof(ChatPage),
        ["sms"]      = typeof(SmsPage),
        ["inbox"]    = typeof(InboxPage),
        ["settings"] = typeof(SettingsPage),
    };

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow(MainWindowViewModel viewModel, UserSettingsService settingsService, SignalRService signalR, AuthService auth)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _settingsService = settingsService;
        _signalR = signalR;

        // Provide HWND to AuthService so WAM can show the dialog anchored to this window
        auth.SetParentWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));

        ViewModel.ThemeChangeRequested += OnThemeChangeRequested;

        // Apply theme from settings and wire key handler in one pass
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = ViewModel.IsDarkMode ? ElementTheme.Dark : ElementTheme.Light;
            root.KeyDown += OnRootKeyDown;
        }

        _confirmButton = new Button { Content = "Potwierdzam przeczytanie" };
        _confirmButton.Click += OnConfirmButtonClick;

        AppWindow.SetIcon("Assets/AppIcon.ico");
        SetupWindow();

        ViewModel.StatusVersion = GetPackageVersion();
        signalR.StateChanged += state => App.DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.StatusSignalRConnected = state == HubConnectionState.Connected;
            ViewModel.StatusSignalR = state switch
            {
                HubConnectionState.Connected    => "Połączony",
                HubConnectionState.Connecting   => "Łączenie…",
                HubConnectionState.Reconnecting => "Ponowne łączenie…",
                _                               => "Rozłączony",
            };
        });

        NavView.Loaded += (_, _) =>
        {
            if (NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is { } first)
                NavView.SelectedItem = first;
        };
    }

    private static string GetPackageVersion()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) is { } ver
                ? $"v{ver}" : string.Empty;
        }
    }

    private void OnThemeChangeRequested(object? sender, ElementTheme theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme;
        _settingsService.SaveTheme(theme == ElementTheme.Dark);
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;
        if (!_pageMap.TryGetValue(tag, out var pageType)) return;

        ContentFrame.Navigate(pageType);

        if (tag == "inbox")
            NavigatedToInbox?.Invoke(this, EventArgs.Empty);

        if (tag == "chat")
            NavigatedToChat?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateTo(string tag)
    {
        var allItems = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>());

        var item = allItems.FirstOrDefault(i => i.Tag is string t && t == tag);
        if (item is not null)
            NavView.SelectedItem = item;
        else if (_pageMap.TryGetValue(tag, out var pageType))
            ContentFrame.Navigate(pageType);
    }

    public void ShowAndNavigateTo(string tag)
    {
        NavigateTo(tag);
        ShowAndActivate();
    }

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }

    public bool IsVisible => AppWindow.IsVisible;

    public void HideWindow()
    {
        SaveCurrentBounds();
        AppWindow.Hide();
    }

    public void EnqueueMessage(MessageDto msg)
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            _messageQueue.Enqueue(msg);
            if (msg.RequiresConfirmation) ShowAndActivate();
            if (!_showingMessage) ShowNextMessage();
        });
    }

    private void ShowNextMessage()
    {
        if (!_messageQueue.TryDequeue(out var msg))
        {
            _showingMessage = false;
            return;
        }

        _showingMessage = true;
        _currentMessageId = msg.Id;

        MessageInfoBar.Title = msg.Title;
        MessageInfoBar.Message = msg.Body;
        MessageInfoBar.Severity = msg.RequiresConfirmation
            ? InfoBarSeverity.Warning
            : InfoBarSeverity.Informational;

        if (msg.RequiresConfirmation)
        {
            MessageInfoBar.IsClosable = false;
            MessageInfoBar.ActionButton = _confirmButton;
        }
        else
        {
            MessageInfoBar.IsClosable = true;
            MessageInfoBar.ActionButton = null;
        }

        MessageInfoBar.IsOpen = true;
    }

    private void OnMessageInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs e)
    {
        if (_currentMessageId > 0)
        {
            var id = _currentMessageId;
            _currentMessageId = -1;
            _ = _signalR.MarkAsReadAsync(id);
        }
        ShowNextMessage();
    }

    private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
    {
        MessageInfoBar.IsOpen = false;
    }

    public void ShowUpdateBar(VersionInfoDto info)
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            UpdateInfoBar.Title = $"Dostępna nowa wersja v{info.Version}";
            UpdateInfoBar.Message = "Pobierz aktualizację, aby korzystać z najnowszych funkcji.";

            if (!string.IsNullOrEmpty(info.DownloadUrl))
            {
                UpdateInfoBar.ActionButton = new HyperlinkButton
                {
                    Content = "Pobierz",
                    NavigateUri = new Uri(info.DownloadUrl),
                };
            }

            UpdateInfoBar.IsOpen = true;
        });
    }

    private void SetupWindow()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hWnd) / 96.0;
        var saved = _settingsService.LoadWindowBounds("Main");

        if (saved is { Width: > 0, Height: > 0 })
        {
            AppWindow.Resize(new SizeInt32((int)(saved.Value.Width * scale), (int)(saved.Value.Height * scale)));
            if (saved.Value.X >= 0 && saved.Value.Y >= 0)
                AppWindow.Move(new PointInt32((int)(saved.Value.X * scale), (int)(saved.Value.Y * scale)));
            else
                CenterWindow();
        }
        else
        {
            AppWindow.Resize(new SizeInt32((int)(1000 * scale), (int)(660 * scale)));
            CenterWindow();
        }

        AppWindow.Title = "digiWORK SmartHUB";
        AppWindow.Closing += OnAppWindowClosing;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        SaveCurrentBounds();
        if (CloseToTray)
        {
            e.Cancel = true;
            AppWindow.Hide();
        }
    }

    private void CenterWindow()
    {
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        AppWindow.Move(new PointInt32(
            work.X + (work.Width  - AppWindow.Size.Width)  / 2,
            work.Y + (work.Height - AppWindow.Size.Height) / 2));
    }

    private void SaveCurrentBounds()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hWnd) / 96.0;
        _settingsService.SaveWindowBounds("Main",
            (int)(AppWindow.Size.Width  / scale),
            (int)(AppWindow.Size.Height / scale),
            (int)(AppWindow.Position.X  / scale),
            (int)(AppWindow.Position.Y  / scale));
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            HideWindow();
            e.Handled = true;
        }
    }
}
