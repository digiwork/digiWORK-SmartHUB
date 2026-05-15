using Dapper;

namespace CompanyDirectory_Desktop.LocalCache;

public class RecentlyViewedRepository(DatabaseInitializer db) : IRecentlyViewedRepository
{
    public async Task RecordViewAsync(string login)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO recently_viewed (Login, ViewedAt) VALUES (@login, @viewedAt)",
            new { login, viewedAt = DateTime.UtcNow.ToString("o") });
    }

    public async Task<List<string>> GetRecentAsync(int limit = 10)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<string>(
            "SELECT Login FROM recently_viewed ORDER BY ViewedAt DESC LIMIT @limit",
            new { limit });
        return [.. result];
    }
}
