using CompanyDirectory_Desktop.LocalCache;
using Microsoft.Extensions.Logging;

namespace CompanyDirectory_Desktop.Services;

public class SyncService(
    IApiClient apiClient,
    IUserCacheRepository repository,
    ToastNotificationService toasts,
    ILogger<SyncService> logger)
{
    public async Task<SyncResult> SyncNowAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("Cache sync started");

        var users = await apiClient.GetAllUsersAsync(ct);

        if (users.Count == 0)
        {
            logger.LogWarning("Sync returned 0 users — API unreachable or empty directory");
            toasts.ShowSyncFailed();
            return new SyncResult(0, sw.Elapsed, Success: false);
        }

        var upserted = await repository.UpsertUsersAsync(users);
        sw.Stop();

        logger.LogInformation("Cache sync completed: {Count} users upserted in {Ms}ms",
            upserted, sw.ElapsedMilliseconds);

        return new SyncResult(upserted, sw.Elapsed, Success: true);
    }
}

public record SyncResult(int Count, TimeSpan Duration, bool Success);
