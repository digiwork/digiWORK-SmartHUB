using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Text.Json;
using Windows.Storage.Streams;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IUserCacheRepository    _repository;
    private readonly IFavoritesRepository    _favorites;
    private readonly IRecentlyViewedRepository _recentlyViewed;
    private readonly int _minQueryLength;
    private readonly int _maxResults;
    private CancellationTokenSource? _searchCts;

    private const string ItGroupDn =
        "CN=IT,OU=Grupy zabezpieczeń,OU=AGROAS,DC=AGROAS,DC=local";

    private readonly bool _currentUserIsItMember;

    // Partial-property syntax required for WinUI 3 AOT / CsWinRT marshalling (MVVMTK0045)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsVisibility))]
    [NotifyPropertyChangedFor(nameof(FormattedEmployeeId))]
    [NotifyPropertyChangedFor(nameof(CardSectionVisibility))]
    [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
    public partial UserDirectoryEntryDto? SelectedUser { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardSectionVisibility))]
    public partial bool ShowEmployeeCard { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteButtonTooltip))]
    public partial bool SelectedUserIsFavorite { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSectionLabel))]
    public partial string SectionLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? SelectedUserPhoto { get; private set; }

    public Visibility ShowSectionLabel =>
        string.IsNullOrEmpty(SectionLabel) ? Visibility.Collapsed : Visibility.Visible;

    public string FavoriteGlyph         => SelectedUserIsFavorite ? "" : "";
    public string FavoriteButtonTooltip => SelectedUserIsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych";

    partial void OnSelectedUserChanged(UserDirectoryEntryDto? value)
    {
        _ = LoadSelectedUserPhotoAsync(value?.PhotoBase64);
        _ = UpdateFavoriteStatusAsync(value);
        if (value is not null)
            _ = _recentlyViewed.RecordViewAsync(value.Login);
    }

    private async Task UpdateFavoriteStatusAsync(UserDirectoryEntryDto? user)
    {
        SelectedUserIsFavorite = user is not null && await _favorites.IsFavoriteAsync(user.Login);
    }

    private async Task LoadSelectedUserPhotoAsync(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            SelectedUserPhoto = null;
            return;
        }
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            writer.DetachStream();
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            SelectedUserPhoto = bitmap;
        }
        catch
        {
            SelectedUserPhoto = null;
        }
    }

    [ObservableProperty] public partial string SearchQuery   { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   IsLoading     { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public ObservableCollection<UserDirectoryEntryDto> Results { get; } = [];

    public List<ExportColumnOption> ExportColumns { get; } =
    [
        new() { Header = "Imię i nazwisko",  Selector = u => u.DisplayName          ?? string.Empty },
        new() { Header = "Login",            Selector = u => u.Login                ?? string.Empty },
        new() { Header = "E-mail",           Selector = u => u.Email                ?? string.Empty },
        new() { Header = "Stanowisko",       Selector = u => u.JobTitle             ?? string.Empty },
        new() { Header = "Dział",            Selector = u => u.Department           ?? string.Empty },
        new() { Header = "Firma",            Selector = u => u.Company              ?? string.Empty },
        new() { Header = "Biuro",            Selector = u => u.Office               ?? string.Empty },
        new() { Header = "Telefon",          Selector = u => u.Phone                ?? string.Empty },
        new() { Header = "Komórka",          Selector = u => u.Mobile               ?? string.Empty },
        new() { Header = "Przełożony",       Selector = u => u.ManagerDisplayName   ?? string.Empty },
    ];

    public Visibility DetailsVisibility
        => SelectedUser != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ItSectionVisibility
        => _currentUserIsItMember ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CardSectionVisibility
        => ShowEmployeeCard && SelectedUser != null ? Visibility.Visible : Visibility.Collapsed;

    public string FormattedEmployeeId
    {
        get
        {
            var id = SelectedUser?.EmployeeId;
            if (string.IsNullOrEmpty(id)) return string.Empty;
            return $"AGRO {id.PadLeft(4, '0')}";
        }
    }

    public SearchViewModel(
        IUserCacheRepository       repository,
        IFavoritesRepository       favorites,
        IRecentlyViewedRepository  recentlyViewed,
        IOptions<SearchSettings>   searchOptions,
        IOptions<UiSettings>       uiOptions)
    {
        _repository              = repository;
        _favorites               = favorites;
        _recentlyViewed          = recentlyViewed;
        _minQueryLength          = searchOptions.Value.MinimumQueryLength;
        _maxResults              = searchOptions.Value.MaxResults;
        _currentUserIsItMember   = CheckCurrentUserItMembership();
        ShowEmployeeCard         = uiOptions.Value.ShowEmployeeCard;

        _ = LoadDefaultItemsAsync();
    }

    private bool CheckCurrentUserItMembership()
    {
        try
        {
            var windowsName = WindowsIdentity.GetCurrent().Name;
            var login = windowsName.Contains('\\')
                ? windowsName.Split('\\')[1]
                : windowsName;

            var user = _repository.GetByLoginAsync(login).GetAwaiter().GetResult();
            if (user is null) return false;

            var groups = JsonSerializer.Deserialize<string[]>(user.Groups) ?? [];
            return groups.Any(g => string.Equals(g, ItGroupDn, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private bool _navigating;

    partial void OnSearchQueryChanged(string value)
    {
        if (_navigating) return;
        _ = PerformSearchAsync(value);
    }

    [RelayCommand]
    private async Task NavigateToManagerAsync()
    {
        var dn = SelectedUser?.ManagerDistinguishedName;
        if (string.IsNullOrEmpty(dn)) return;

        var manager = await _repository.GetByDistinguishedNameAsync(dn);
        if (manager is null) return;

        _navigating = true;
        SearchQuery = manager.DisplayName;
        _navigating = false;

        Results.Clear();
        Results.Add(manager);
        SelectedUser  = manager;
        StatusMessage = "Znaleziono 1 wyników";
    }

    [RelayCommand]
    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }

    [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
    private async Task ToggleFavoriteAsync()
    {
        var user = SelectedUser;
        if (user is null) return;

        if (SelectedUserIsFavorite)
        {
            await _favorites.RemoveAsync(user.Login);
            SelectedUserIsFavorite = false;
        }
        else
        {
            await _favorites.AddAsync(user.Login);
            SelectedUserIsFavorite = true;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
            await LoadDefaultItemsAsync();
    }

    private bool CanToggleFavorite() => SelectedUser is not null;

    public async Task ToggleFavoriteForUserAsync(UserDirectoryEntryDto user)
    {
        var isFav = await _favorites.IsFavoriteAsync(user.Login);
        if (isFav)
            await _favorites.RemoveAsync(user.Login);
        else
            await _favorites.AddAsync(user.Login);

        if (SelectedUser?.Login == user.Login)
            SelectedUserIsFavorite = !isFav;

        if (string.IsNullOrWhiteSpace(SearchQuery))
            await LoadDefaultItemsAsync();
    }

    public void NavigateToUser(UserDirectoryEntryDto user)
    {
        _navigating  = true;
        SearchQuery  = user.DisplayName;
        _navigating  = false;

        Results.Clear();
        Results.Add(user);
        SelectedUser  = user;
        SectionLabel  = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Clear()
    {
        SearchQuery   = string.Empty;
        SelectedUser  = null;
        StatusMessage = string.Empty;
        Results.Clear();
    }

    private async Task PerformSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            await Task.Delay(300, ct);

            if (string.IsNullOrWhiteSpace(query))
            {
                await LoadDefaultItemsAsync();
                return;
            }

            SectionLabel = string.Empty;

            if (query.Length < _minQueryLength)
            {
                Results.Clear();
                StatusMessage = $"Wpisz co najmniej {_minQueryLength} znaki";
                return;
            }

            IsLoading = true;
            var items = await _repository.SearchAsync(query, _maxResults);

            ct.ThrowIfCancellationRequested();

            Results.Clear();
            foreach (var item in items)
                Results.Add(item);

            StatusMessage = Results.Count > 0
                ? $"Znaleziono {Results.Count} wyników"
                : "Brak wyników";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task LoadDefaultItemsAsync()
    {
        Results.Clear();
        SelectedUser = null;

        var favoriteLogins = await _favorites.GetAllAsync();
        if (favoriteLogins.Count > 0)
        {
            SectionLabel = "Ulubieni";
            foreach (var login in favoriteLogins)
            {
                var user = await _repository.GetByLoginAsync(login);
                if (user is not null) Results.Add(user);
            }
            StatusMessage = Results.Count > 0 ? $"{Results.Count} ulubionych" : string.Empty;
            return;
        }

        var recentLogins = await _recentlyViewed.GetRecentAsync();
        if (recentLogins.Count > 0)
        {
            SectionLabel = "Ostatnio oglądani";
            foreach (var login in recentLogins)
            {
                var user = await _repository.GetByLoginAsync(login);
                if (user is not null) Results.Add(user);
            }
            StatusMessage = Results.Count > 0 ? $"{Results.Count} ostatnio oglądanych" : string.Empty;
            return;
        }

        SectionLabel  = string.Empty;
        StatusMessage = string.Empty;
    }
}
