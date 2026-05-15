using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyDirectory_Desktop.Services;

public sealed class SyncBackgroundService : IDisposable
{
    private readonly SyncService _sync;
    private readonly SyncSettings _settings;
    private readonly ILogger<SyncBackgroundService> _logger;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    public SyncBackgroundService(
        SyncService sync,
        IOptions<SyncSettings> options,
        ILogger<SyncBackgroundService> logger)
    {
        _sync     = sync;
        _settings = options.Value;
        _logger   = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var thread = new Thread(() => RunAsync(_cts.Token).GetAwaiter().GetResult())
        {
            IsBackground = true,
            Name = "SyncBackgroundThread",
        };
        thread.Start();
        _logger.LogInformation("SyncBackgroundService started (interval: {Min} min, syncOnStartup: {Startup})",
            _settings.IntervalMinutes, _settings.SyncOnStartup);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            if (_settings.SyncOnStartup)
            {
                // Brief delay so the UI is visible before the first sync
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await _sync.SyncNowAsync(ct);
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.IntervalMinutes));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await _sync.SyncNowAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in SyncBackgroundService");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _logger.LogInformation("SyncBackgroundService stopped");
    }
}
