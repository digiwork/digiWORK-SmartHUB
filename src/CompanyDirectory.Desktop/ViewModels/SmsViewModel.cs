using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using CompanyDirectory_Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class SmsViewModel(
    IUserCacheRepository          repository,
    IApiClient                    apiClient,
    IOptions<SmsSettings>         smsOptions,
    ILogger<SmsViewModel>         logger) : ObservableObject
{
    private readonly SmsSettings _cfg = smsOptions.Value;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchResultsVisibility))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharCountDisplay))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public ObservableCollection<SmsRecipientEntry>   Recipients    { get; } = [];
    public ObservableCollection<UserDirectoryEntryDto> SearchResults { get; } = [];

    public int    CharCount   => MessageText.Length;
    public int    SmsCount    => MessageText.Length == 0 ? 0 : MessageText.Length <= 160 ? 1 : (int)Math.Ceiling(MessageText.Length / 153.0);
    public string CharCountDisplay => $"{CharCount}/160  ·  {SmsCount} SMS";

    public Visibility SearchResultsVisibility =>
        SearchResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public int    RecipientWithMobileCount => Recipients.Count(r => r.HasMobile);
    public string SendButtonText           => $"Wyślij do {RecipientWithMobileCount} odbiorców";
    public string SenderDisplay            => $"Nadawca: {_cfg.SenderName}";

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(value);

    private void NotifyRecipientsChanged()
    {
        OnPropertyChanged(nameof(RecipientWithMobileCount));
        OnPropertyChanged(nameof(SendButtonText));
        SendCommand.NotifyCanExecuteChanged();
    }

    public void AddRecipient(UserDirectoryEntryDto user)
    {
        if (Recipients.Any(r => r.User.Login == user.Login)) return;
        Recipients.Add(new SmsRecipientEntry { User = user });
        NotifyRecipientsChanged();
    }

    public void RemoveRecipient(SmsRecipientEntry entry)
    {
        Recipients.Remove(entry);
        NotifyRecipientsChanged();
    }

    [RelayCommand]
    private void SelectClear()
    {
        Recipients.Clear();
        NotifyRecipientsChanged();
    }

    public void PopulateRecipients(IEnumerable<UserDirectoryEntryDto> users)
    {
        Recipients.Clear();
        foreach (var u in users)
            Recipients.Add(new SmsRecipientEntry { User = u });
        NotifyRecipientsChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var phones = Recipients
            .Where(r => r.HasMobile)
            .Select(r => r.User.Mobile!)
            .ToList();

        IsBusy        = true;
        StatusMessage = $"Wysyłanie do {phones.Count} odbiorców…";
        try
        {
            var result = await apiClient.SendSmsAsync(new SmsSendDto
            {
                PhoneNumbers = phones,
                Message      = MessageText,
            });

            StatusMessage = string.IsNullOrEmpty(result.ErrorMessage)
                ? $"Wysłano: {result.Sent}, błędy: {result.Failed}"
                : $"Błąd: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS send failed");
            StatusMessage = "Błąd wysyłania — sprawdź konfigurację bramki SMS";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(MessageText) &&
        RecipientWithMobileCount > 0;

    private async Task SearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            await Task.Delay(300, ct);

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                SearchResults.Clear();
                OnPropertyChanged(nameof(SearchResultsVisibility));
                return;
            }

            var results = await repository.SearchAsync(query, maxResults: 10);
            ct.ThrowIfCancellationRequested();

            SearchResults.Clear();
            foreach (var u in results) SearchResults.Add(u);
            OnPropertyChanged(nameof(SearchResultsVisibility));
        }
        catch (OperationCanceledException) { }
    }
}
