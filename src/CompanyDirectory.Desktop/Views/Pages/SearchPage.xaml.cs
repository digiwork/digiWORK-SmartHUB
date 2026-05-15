using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.Services;
using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using Windows.System;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class SearchPage : Page
{
    public SearchViewModel ViewModel { get; }

    private readonly ExportService _exportService;

    public SearchPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SearchViewModel>();
        _exportService = App.Services.GetRequiredService<ExportService>();
    }

    private async void OnChatButtonClick(object sender, RoutedEventArgs e)
    {
        var user = ViewModel.SelectedUser;
        if (user is null) return;
        await OpenChatWithAsync(user);
    }

    // ── Karta: szybkie akcje ──────────────────────────────────────────────

    private async void OnCardChatClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: UserDirectoryEntryDto user })
            await OpenChatWithAsync(user);
    }

    private void OnCardSmsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: UserDirectoryEntryDto user }) return;
        App.Services.GetRequiredService<SmsViewModel>().AddRecipient(user);
        App.Services.GetRequiredService<MainWindow>().NavigateTo("sms");
    }

    private async void OnCardEmailClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: UserDirectoryEntryDto user }) return;
        if (string.IsNullOrEmpty(user.Email)) return;
        await Launcher.LaunchUriAsync(new Uri($"mailto:{user.Email}"));
    }

    private async void OnCardFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: UserDirectoryEntryDto user }) return;
        await ViewModel.ToggleFavoriteForUserAsync(user);
    }

    private async Task OpenChatWithAsync(UserDirectoryEntryDto user)
    {
        var chatVm = App.Services.GetRequiredService<ChatViewModel>();
        await chatVm.OpenConversationAsync(user.Login, user.DisplayName);
        App.Services.GetRequiredService<MainWindow>().NavigateTo("chat");
    }

    private async void OnExportButtonClick(object sender, RoutedEventArgs e)
    {
        var cols = ViewModel.ExportColumns;

        var panel = new StackPanel { Spacing = 8 };
        var checkBoxes = new List<CheckBox>();
        foreach (var col in cols)
        {
            var cb = new CheckBox { Content = col.Header, IsChecked = col.IsSelected };
            checkBoxes.Add(cb);
            panel.Children.Add(cb);
        }

        var dialog = new ContentDialog
        {
            Title             = "Wybierz kolumny do eksportu",
            Content           = new ScrollViewer { Content = panel, MaxHeight = 340 },
            PrimaryButtonText = "Eksportuj",
            CloseButtonText   = "Anuluj",
            XamlRoot          = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        for (var i = 0; i < cols.Count; i++)
            cols[i].IsSelected = checkBoxes[i].IsChecked == true;

        var selectedCols = cols.Where(c => c.IsSelected).ToList();
        if (selectedCols.Count == 0) return;

        var picker = new FileSavePicker();
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(mainWindow));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName      = $"eksport_{DateTime.Now:yyyyMMdd_HHmm}";
        picker.FileTypeChoices.Add("CSV", [".csv"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await _exportService.ExportToCsvAsync(ViewModel.Results.ToList(), selectedCols, file);
    }

    private void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down && ResultsListView.Items.Count > 0)
        {
            ResultsListView.Focus(FocusState.Keyboard);
            if (ResultsListView.SelectedIndex < 0)
                ResultsListView.SelectedIndex = 0;
            e.Handled = true;
        }
    }

    private void OnResultsListViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up && ResultsListView.SelectedIndex <= 0)
        {
            SearchTextBox.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
    }
}
