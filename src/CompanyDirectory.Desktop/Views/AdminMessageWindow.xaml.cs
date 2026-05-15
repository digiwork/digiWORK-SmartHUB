using CompanyDirectory_Desktop.Services;
using CompanyDirectory_Desktop.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace CompanyDirectory_Desktop.Views;

public sealed partial class AdminMessageWindow : Window
{
    public AdminMessageViewModel ViewModel { get; }

    private AppWindow? _appWindow;
    private readonly UserSettingsService _settingsService;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public AdminMessageWindow(AdminMessageViewModel viewModel, UserSettingsService settingsService)
    {
        InitializeComponent();
        ViewModel        = viewModel;
        _settingsService = settingsService;
        SetupWindow();
    }

    private void SetupWindow()
    {
        var hWnd     = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow   = AppWindow.GetFromWindowId(windowId);

        var scale = GetDpiForWindow(hWnd) / 96.0;
        var saved = _settingsService.LoadWindowBounds("Admin");

        if (saved is { Width: > 0, Height: > 0 })
        {
            _appWindow.Resize(new SizeInt32((int)(saved.Value.Width * scale), (int)(saved.Value.Height * scale)));
            if (saved.Value.X >= 0 && saved.Value.Y >= 0)
                _appWindow.Move(new PointInt32((int)(saved.Value.X * scale), (int)(saved.Value.Y * scale)));
            else
                CenterWindow(_appWindow);
        }
        else
        {
            _appWindow.Resize(new SizeInt32((int)(480 * scale), (int)(400 * scale)));
            CenterWindow(_appWindow);
        }

        _appWindow.Title = "Wyślij wiadomość — CompanyDirectory";
        _appWindow.Closing += OnAppWindowClosing;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        SaveCurrentBounds();
        e.Cancel = true;
        _appWindow?.Hide();
    }

    private static void CenterWindow(AppWindow appWindow)
    {
        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var work    = display.WorkArea;
        appWindow.Move(new PointInt32(
            work.X + (work.Width  - appWindow.Size.Width)  / 2,
            work.Y + (work.Height - appWindow.Size.Height) / 2));
    }

    public void ShowAndActivate()
    {
        _appWindow?.Show();
        Activate();
    }

    public void HideWindow()
    {
        SaveCurrentBounds();
        _appWindow?.Hide();
    }

    private void SaveCurrentBounds()
    {
        if (_appWindow is null) return;
        var hWnd  = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hWnd) / 96.0;
        var size  = _appWindow.Size;
        var pos   = _appWindow.Position;
        _settingsService.SaveWindowBounds("Admin",
            (int)(size.Width  / scale),
            (int)(size.Height / scale),
            (int)(pos.X       / scale),
            (int)(pos.Y       / scale));
    }
}
