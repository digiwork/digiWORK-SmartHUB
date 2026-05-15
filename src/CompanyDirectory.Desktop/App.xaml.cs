using CompanyDirectory.Shared.Configuration;
using CompanyDirectory_Desktop.Activation;
using CompanyDirectory_Desktop.LocalCache;
using CompanyDirectory_Desktop.Services;
using CompanyDirectory_Desktop.ViewModels;
using CompanyDirectory_Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using System.IO;

namespace CompanyDirectory_Desktop;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Initialize database synchronously before showing UI
        Services.GetRequiredService<DatabaseInitializer>().Initialize();

        var activation = Services.GetRequiredService<AppActivationService>();
        _ = activation.ActivateAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configuration — user settings in LocalAppData overlay app defaults
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var userSettingsPath = Path.Combine(
            Environment.ExpandEnvironmentVariables("%LocalAppData%"),
            "CompanyDirectory", "usersettings.json");

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath,      optional: false, reloadOnChange: false)
            .AddJsonFile(userSettingsPath, optional: true,  reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(config);

        // Bind settings sections
        services.Configure<LocalCacheSettings>(config.GetSection(LocalCacheSettings.SectionName));
        services.Configure<ApiClientSettings>(config.GetSection(ApiClientSettings.SectionName));
        services.Configure<SyncSettings>(config.GetSection(SyncSettings.SectionName));
        services.Configure<HotkeySettings>(config.GetSection(HotkeySettings.SectionName));
        services.Configure<UiSettings>(config.GetSection(UiSettings.SectionName));
        services.Configure<SearchSettings>(config.GetSection(SearchSettings.SectionName));
        services.Configure<SmsSettings>(config.GetSection(SmsSettings.SectionName));
        services.Configure<OrgChartSettings>(config.GetSection(OrgChartSettings.SectionName));
        services.Configure<AzureAdSettings>(config.GetSection(AzureAdSettings.SectionName));

        // Serilog
        var logDir = Path.Combine(
            Environment.ExpandEnvironmentVariables("%LocalAppData%"),
            "CompanyDirectory", "Logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false));

        // Infrastructure
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IUserCacheRepository, UserCacheRepository>();
        services.AddSingleton<IFavoritesRepository, FavoritesRepository>();
        services.AddSingleton<IRecentlyViewedRepository, RecentlyViewedRepository>();

        // Application services
        services.AddSingleton<AuthService>();
        services.AddSingleton<SeedDataService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<SyncBackgroundService>();
        services.AddSingleton<AutostartService>();
        services.AddSingleton<UserSettingsService>();
        services.AddSingleton<ToastNotificationService>();
        services.AddSingleton<SignalRService>();
        services.AddSingleton<ExportService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AdminMessageViewModel>();
        services.AddSingleton<InboxViewModel>();
        services.AddSingleton<SentMessagesViewModel>();
        services.AddSingleton<SmsViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<OrgChartViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<RecentViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<AdminMessageWindow>();

        // Activation
        services.AddSingleton<AppActivationService>();
    }
}
