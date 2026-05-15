using CompanyDirectory.Shared.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnreadBadgeVisibility))]
    public partial int UnreadCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChatUnreadBadgeVisibility))]
    public partial int ChatUnreadCount { get; set; }

    [ObservableProperty]
    public partial bool IsDarkMode { get; set; }

    [ObservableProperty]
    public partial string StatusVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusSignalR { get; set; } = "Rozłączony";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDotBrush))]
    public partial bool StatusSignalRConnected { get; set; }

    public SolidColorBrush StatusDotBrush => new(StatusSignalRConnected
        ? Colors.SeaGreen
        : Color.FromArgb(255, 138, 136, 134));

    public Visibility UnreadBadgeVisibility =>
        UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChatUnreadBadgeVisibility =>
        ChatUnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public event EventHandler<ElementTheme>? ThemeChangeRequested;

    public MainWindowViewModel(IOptions<UiSettings> uiOptions)
    {
        IsDarkMode = uiOptions.Value.Theme == "Dark";
    }

    partial void OnIsDarkModeChanged(bool value) =>
        ThemeChangeRequested?.Invoke(this, value ? ElementTheme.Dark : ElementTheme.Light);
}
