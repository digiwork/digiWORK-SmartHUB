using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class SentMessagesViewModel(
    IApiClient apiClient,
    ILogger<SentMessagesViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsVisibility))]
    public partial MessageDto? SelectedMessage { get; set; }

    [ObservableProperty] public partial bool   IsBusy        { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public ObservableCollection<MessageDto> Messages { get; } = [];

    public Visibility DetailsVisibility =>
        SelectedMessage is not null ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy        = true;
        StatusMessage = "Ładowanie…";
        try
        {
            var msgs = await apiClient.GetSentMessagesAsync();
            Messages.Clear();
            foreach (var m in msgs) Messages.Add(m);

            if (Messages.Count > 0)
                StatusMessage = $"{Messages.Count} wysłanych wiadomości";
            else
                StatusMessage = msgs.Count == 0
                    ? "Brak wysłanych wiadomości (lub brak uprawnień)"
                    : "Brak wysłanych wiadomości";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load sent messages");
            StatusMessage = "Błąd ładowania — sprawdź połączenie z API";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
