using CompanyDirectory.Shared.Dtos;
using CompanyDirectory.Shared.TextUtils;
using Dapper;

namespace CompanyDirectory_Desktop.LocalCache;

public class UserCacheRepository(DatabaseInitializer db) : IUserCacheRepository
{
    public async Task<int> UpsertUsersAsync(IEnumerable<UserDirectoryEntryDto> users)
    {
        var list = users.ToList();
        if (list.Count == 0) return 0;

        using var connection = db.OpenConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT OR REPLACE INTO users
                (Login, UserPrincipalName, DisplayName, FirstName, LastName,
                 Email, Phone, Mobile, JobTitle, Department, Company, Office,
                 ManagerDistinguishedName, ManagerDisplayName, DistinguishedName,
                 Description, Groups, PhotoBase64, EmployeeId, Sid, IsActive,
                 CreatedInAd, ModifiedInAd, LastSyncedAt, SearchText)
            VALUES
                (@Login, @UserPrincipalName, @DisplayName, @FirstName, @LastName,
                 @Email, @Phone, @Mobile, @JobTitle, @Department, @Company, @Office,
                 @ManagerDistinguishedName, @ManagerDisplayName, @DistinguishedName,
                 @Description, @Groups, @PhotoBase64, @EmployeeId, @Sid, @IsActive,
                 @CreatedInAd, @ModifiedInAd, @LastSyncedAt, @SearchText)";

        var rows = 0;
        using var tx = await connection.BeginTransactionAsync();
        foreach (var u in list)
        {
            rows += await connection.ExecuteAsync(sql, new
            {
                u.Login, u.UserPrincipalName, u.DisplayName, u.FirstName, u.LastName,
                u.Email, u.Phone, u.Mobile, u.JobTitle, u.Department, u.Company, u.Office,
                u.ManagerDistinguishedName, u.ManagerDisplayName, u.DistinguishedName,
                u.Description, u.Groups, u.PhotoBase64, u.EmployeeId, u.Sid,
                IsActive = u.IsActive ? 1 : 0,
                CreatedInAd  = u.CreatedInAd?.ToString("o"),
                ModifiedInAd = u.ModifiedInAd?.ToString("o"),
                LastSyncedAt = u.LastSyncedAt.ToString("o"),
                SearchText   = BuildSearchText(u)
            }, transaction: tx);
        }
        await tx.CommitAsync();
        return rows;
    }

    public async Task<List<UserDirectoryEntryDto>> SearchAsync(string query, int maxResults)
    {
        var normalized = NormalizePolish(query.ToLowerInvariant().Trim());
        var cardQuery  = NormalizeCardQuery(query.Trim());

        // Always search by EmployeeId exact match; card numbers don't collide with text search.
        const string p1Card = "OR EmployeeId = @cardExact";

        var sql = $"""
            SELECT *, 1 AS _rank FROM users
                WHERE lower(Login) = @exact OR lower(Email) = @exact {p1Card}
            UNION ALL
            SELECT *, 2 AS _rank FROM users
                WHERE (SearchText LIKE @starts OR SearchText LIKE '% ' || @starts)
                  AND NOT (lower(Login) = @exact OR lower(Email) = @exact {p1Card})
            UNION ALL
            SELECT *, 3 AS _rank FROM users
                WHERE SearchText LIKE @contains
                  AND NOT (lower(Login) = @exact OR lower(Email) = @exact {p1Card})
                  AND NOT (SearchText LIKE @starts OR SearchText LIKE '% ' || @starts)
            ORDER BY _rank, DisplayName
            LIMIT @limit
            """;

        using var connection = db.OpenConnection();
        await connection.OpenAsync();

        var results = await connection.QueryAsync<UserCacheRow>(sql, new
        {
            exact     = normalized,
            starts    = normalized + "%",
            contains  = "%" + normalized + "%",
            limit     = maxResults,
            cardExact = cardQuery,
        });

        return [.. results.Select(MapRow)];
    }

    // "0042"→"42", "AGRO 0042"→"42", "AGRO0042"→"42", non-numeric→unchanged
    private static string NormalizeCardQuery(string query)
    {
        var q = query.Trim();
        if (q.StartsWith("AGRO", StringComparison.OrdinalIgnoreCase))
            q = q[4..].TrimStart();
        if (q.Length > 0 && q.All(char.IsDigit))
        {
            var stripped = q.TrimStart('0');
            return stripped.Length > 0 ? stripped : "0";
        }
        return q;
    }

    public async Task<List<UserDirectoryEntryDto>> GetAllActiveAsync(string? companyFilter = null)
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();

        var sql = string.IsNullOrWhiteSpace(companyFilter)
            ? "SELECT * FROM users WHERE IsActive = 1 ORDER BY DisplayName"
            : "SELECT * FROM users WHERE IsActive = 1 AND Company = @company ORDER BY DisplayName";

        var rows = await connection.QueryAsync<UserCacheRow>(
            sql, string.IsNullOrWhiteSpace(companyFilter) ? null : new { company = companyFilter });
        return [.. rows.Select(MapRow)];
    }

    public async Task<UserDirectoryEntryDto?> GetByLoginAsync(string login)
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();
        var row = await connection.QueryFirstOrDefaultAsync<UserCacheRow>(
            "SELECT * FROM users WHERE Login = @login", new { login });
        return row is null ? null : MapRow(row);
    }

    public async Task<UserDirectoryEntryDto?> GetByDistinguishedNameAsync(string dn)
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();
        var row = await connection.QueryFirstOrDefaultAsync<UserCacheRow>(
            "SELECT * FROM users WHERE DistinguishedName = @dn", new { dn });
        return row is null ? null : MapRow(row);
    }

    public async Task<int> GetUserCountAsync()
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
    }

    public async Task ClearAsync()
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM users");
    }

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        using var connection = db.OpenConnection();
        await connection.OpenAsync();
        var raw = await connection.ExecuteScalarAsync<string?>(
            "SELECT MAX(LastSyncedAt) FROM users");
        return raw is null ? null : DateTime.Parse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private static string BuildSearchText(UserDirectoryEntryDto u)
        => NormalizePolish(string.Join(" ", u.Login, u.DisplayName, u.FirstName,
            u.LastName, u.Email, u.Department, u.JobTitle, u.Office, u.Phone, u.Mobile)
            .ToLowerInvariant());

    internal static string NormalizePolish(string input)
        => PolishTextNormalizer.Normalize(input);

    private static UserDirectoryEntryDto MapRow(UserCacheRow r) => new()
    {
        Id                       = r.Id,
        Login                    = r.Login,
        UserPrincipalName        = r.UserPrincipalName,
        DisplayName              = r.DisplayName,
        FirstName                = r.FirstName,
        LastName                 = r.LastName,
        Email                    = r.Email,
        Phone                    = r.Phone,
        Mobile                   = r.Mobile,
        JobTitle                 = r.JobTitle,
        Department               = r.Department,
        Company                  = r.Company,
        Office                   = r.Office,
        ManagerDistinguishedName = r.ManagerDistinguishedName,
        ManagerDisplayName       = r.ManagerDisplayName,
        DistinguishedName        = r.DistinguishedName,
        Description              = r.Description,
        Groups                   = r.Groups,
        PhotoBase64              = r.PhotoBase64,
        EmployeeId               = r.EmployeeId,
        Sid                      = r.Sid,
        IsActive                 = r.IsActive == 1,
        CreatedInAd              = r.CreatedInAd  is null ? null : DateTime.Parse(r.CreatedInAd,  null, System.Globalization.DateTimeStyles.RoundtripKind),
        ModifiedInAd             = r.ModifiedInAd is null ? null : DateTime.Parse(r.ModifiedInAd, null, System.Globalization.DateTimeStyles.RoundtripKind),
        LastSyncedAt             = DateTime.Parse(r.LastSyncedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
    };

    // Internal flat row used by Dapper — avoids mapping issues with nullable DateTime
    private class UserCacheRow
    {
        public int     Id { get; set; }
        public string  Login { get; set; } = "";
        public string  UserPrincipalName { get; set; } = "";
        public string  DisplayName { get; set; } = "";
        public string  FirstName { get; set; } = "";
        public string  LastName { get; set; } = "";
        public string  Email { get; set; } = "";
        public string  Phone { get; set; } = "";
        public string  Mobile { get; set; } = "";
        public string  JobTitle { get; set; } = "";
        public string  Department { get; set; } = "";
        public string  Company { get; set; } = "";
        public string  Office { get; set; } = "";
        public string  ManagerDistinguishedName { get; set; } = "";
        public string  ManagerDisplayName { get; set; } = "";
        public string  DistinguishedName { get; set; } = "";
        public string  Description { get; set; } = "";
        public string  Groups { get; set; } = "[]";
        public string  PhotoBase64 { get; set; } = "";
        public string  EmployeeId { get; set; } = "";
        public string  Sid { get; set; } = "";
        public int     IsActive { get; set; }
        public string? CreatedInAd { get; set; }
        public string? ModifiedInAd { get; set; }
        public string  LastSyncedAt { get; set; } = "";
    }
}
