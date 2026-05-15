using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory_Desktop.LocalCache;
using CompanyDirectory_Desktop.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AutostartService      _autostart;
    private readonly UserSettingsService   _userSettings;
    private readonly MainWindow            _mainWindow;
    private readonly SearchViewModel       _searchViewModel;
    private readonly SyncService           _syncService;
    private readonly SignalRService        _signalR;
    private readonly IUserCacheRepository  _userCache;
    private readonly DatabaseInitializer   _dbInit;

    [ObservableProperty] public partial bool   IsAutostartEnabled   { get; set; }
    [ObservableProperty] public partial bool   StartMinimizedToTray { get; set; }
    [ObservableProperty] public partial bool   CloseToTray          { get; set; }
    [ObservableProperty] public partial bool   ShowEmployeeCard     { get; set; }
    [ObservableProperty] public partial string HotkeyModifiers      { get; set; } = string.Empty;
    [ObservableProperty] public partial string HotkeyKey            { get; set; } = string.Empty;
    [ObservableProperty] public partial int    SyncIntervalMinutes  { get; set; }
    [ObservableProperty] public partial string ApiBaseUrl           { get; set; } = string.Empty;
    [ObservableProperty] public partial string SyncStatus           { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearAndSyncCommand))]
    public partial bool IsSyncing { get; set; }

    // ── Diagnostyka ────────────────────────────────────────────────────────
    [ObservableProperty] public partial string AppVersion    { get; set; } = string.Empty;
    [ObservableProperty] public partial string SignalRStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial string DatabasePath  { get; set; } = string.Empty;
    [ObservableProperty] public partial string LastSyncTime  { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserCount     { get; set; } = string.Empty;
    public string LogsFolderPath { get; } = Path.Combine(
        Environment.ExpandEnvironmentVariables("%LocalAppData%"), "CompanyDirectory", "Logs");

    public event EventHandler? CloseRequested;

    public SettingsViewModel(
        AutostartService            autostart,
        UserSettingsService         userSettings,
        MainWindow                  mainWindow,
        SearchViewModel             searchViewModel,
        SyncService                 syncService,
        SignalRService              signalR,
        IUserCacheRepository        userCache,
        DatabaseInitializer         dbInit,
        IOptions<UiSettings>        uiOptions,
        IOptions<HotkeySettings>    hotkeyOptions,
        IOptions<SyncSettings>      syncOptions,
        IOptions<ApiClientSettings> apiOptions)
    {
        _autostart       = autostart;
        _userSettings    = userSettings;
        _mainWindow      = mainWindow;
        _searchViewModel = searchViewModel;
        _syncService     = syncService;
        _signalR         = signalR;
        _userCache       = userCache;
        _dbInit          = dbInit;

        IsAutostartEnabled   = autostart.IsEnabled;
        StartMinimizedToTray = uiOptions.Value.StartMinimizedToTray;
        CloseToTray          = uiOptions.Value.CloseToTray;
        ShowEmployeeCard     = uiOptions.Value.ShowEmployeeCard;
        HotkeyModifiers      = hotkeyOptions.Value.Modifiers;
        HotkeyKey            = hotkeyOptions.Value.Key;
        SyncIntervalMinutes  = syncOptions.Value.IntervalMinutes;
        ApiBaseUrl           = apiOptions.Value.BaseUrl;

        _ = LoadDiagnosticsAsync();
    }

    [RelayCommand]
    private async Task RefreshDiagnosticsAsync() => await LoadDiagnosticsAsync();

    private async Task LoadDiagnosticsAsync()
    {
        AppVersion    = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "—";
        DatabasePath  = _dbInit.DatabasePath;
        SignalRStatus = _signalR.State switch
        {
            HubConnectionState.Connected    => "Połączony",
            HubConnectionState.Connecting   => "Łączenie…",
            HubConnectionState.Reconnecting => "Ponowne łączenie…",
            _                               => "Rozłączony",
        };

        try
        {
            var lastSync = await _userCache.GetLastSyncTimeAsync();
            LastSyncTime = lastSync.HasValue
                ? lastSync.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "brak danych";

            var count = await _userCache.GetUserCountAsync();
            UserCount = count.ToString();
        }
        catch
        {
            LastSyncTime = "—";
            UserCount    = "—";
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        if (Directory.Exists(LogsFolderPath))
            Process.Start(new ProcessStartInfo("explorer.exe", LogsFolderPath)
                { UseShellExecute = true });
    }

    [RelayCommand]
    private void Save()
    {
        _autostart.SetEnabled(IsAutostartEnabled);
        _mainWindow.CloseToTray                = CloseToTray;
        _searchViewModel.ShowEmployeeCard      = ShowEmployeeCard;

        _userSettings.Save(
            StartMinimizedToTray,
            CloseToTray,
            ShowEmployeeCard,
            HotkeyModifiers,
            HotkeyKey,
            SyncIntervalMinutes,
            ApiBaseUrl);

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private async Task SyncNowAsync()
    {
        IsSyncing  = true;
        SyncStatus = "Synchronizacja…";
        try
        {
            var result = await _syncService.SyncNowAsync();
            SyncStatus = result.Success
                ? $"Zsynchronizowano {result.Count} użytkowników ({(int)result.Duration.TotalSeconds} s)"
                : "Błąd synchronizacji — sprawdź połączenie z API";
        }
        catch (Exception ex)
        {
            SyncStatus = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private async Task ClearAndSyncAsync()
    {
        IsSyncing  = true;
        SyncStatus = "Czyszczenie lokalnej bazy…";
        try
        {
            await _userCache.ClearAsync();
            SyncStatus = "Pobieranie danych z AD…";
            var result = await _syncService.SyncNowAsync();
            SyncStatus = result.Success
                ? $"Gotowe — {result.Count} użytkowników ({(int)result.Duration.TotalSeconds} s)"
                : "Błąd synchronizacji — sprawdź połączenie z API";
            await LoadDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            SyncStatus = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSyncNow() => !IsSyncing;
}
