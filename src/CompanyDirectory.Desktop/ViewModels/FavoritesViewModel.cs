using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using System.Collections.ObjectModel;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly IFavoritesRepository  _favorites;
    private readonly IUserCacheRepository  _cache;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial ObservableCollection<UserDirectoryEntryDto> Users { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool IsEmpty => Users.Count == 0;

    public IAsyncRelayCommand LoadCommand { get; }

    public FavoritesViewModel(IFavoritesRepository favorites, IUserCacheRepository cache)
    {
        _favorites = favorites;
        _cache     = cache;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            var logins = await _favorites.GetAllAsync();
            var users  = new ObservableCollection<UserDirectoryEntryDto>();

            foreach (var login in logins)
            {
                var user = await _cache.GetByLoginAsync(login);
                if (user is not null)
                    users.Add(user);
            }

            Users = users;
            StatusMessage = users.Count == 0 ? string.Empty : $"{users.Count} ulubionych";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
