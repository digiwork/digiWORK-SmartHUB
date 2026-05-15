using Dapper;

namespace CompanyDirectory_Desktop.LocalCache;

public class FavoritesRepository(DatabaseInitializer db) : IFavoritesRepository
{
    public async Task AddAsync(string login)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT OR IGNORE INTO favorites (Login, AddedAt) VALUES (@login, @addedAt)",
            new { login, addedAt = DateTime.UtcNow.ToString("o") });
    }

    public async Task RemoveAsync(string login)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "DELETE FROM favorites WHERE Login = @login",
            new { login });
    }

    public async Task<List<string>> GetAllAsync()
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<string>(
            "SELECT Login FROM favorites ORDER BY AddedAt");
        return [.. result];
    }

    public async Task<bool> IsFavoriteAsync(string login)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM favorites WHERE Login = @login",
            new { login });
        return count > 0;
    }
}
