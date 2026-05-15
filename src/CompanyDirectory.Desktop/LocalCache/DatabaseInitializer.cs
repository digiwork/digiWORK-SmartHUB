using CompanyDirectory.Shared.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyDirectory_Desktop.LocalCache;

public class DatabaseInitializer(
    IOptions<LocalCacheSettings> options,
    ILogger<DatabaseInitializer> logger)
{
    private readonly string _dbPath = Environment.ExpandEnvironmentVariables(options.Value.DatabasePath);

    public string DatabasePath => _dbPath;

    public void Initialize()
    {
        var dir = Path.GetDirectoryName(_dbPath)!;
        Directory.CreateDirectory(dir);

        using var connection = OpenConnection();
        ApplyPragmas(connection);
        EnsureSchemaVersionTable(connection);
        RunMigrations(connection);

        logger.LogInformation("Database initialized at {Path}", _dbPath);
    }

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;Foreign Keys=True");
        return conn;
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        // WAL mode: better read/write concurrency and crash safety
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute("PRAGMA synchronous=NORMAL;");
        connection.Execute("PRAGMA cache_size=-8000;"); // 8 MB page cache
        connection.Execute("PRAGMA temp_store=MEMORY;");
    }

    private static void EnsureSchemaVersionTable(SqliteConnection connection)
    {
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS __schema_version (
                Version    INTEGER PRIMARY KEY,
                AppliedAt  TEXT NOT NULL
            );");
    }

    private void RunMigrations(SqliteConnection connection)
    {
        var applied = connection
            .Query<int>("SELECT Version FROM __schema_version")
            .ToHashSet();

        foreach (var (version, sql) in Migrations)
        {
            if (applied.Contains(version)) continue;

            logger.LogInformation("Applying DB migration v{Version}", version);
            connection.Execute(sql);
            connection.Execute(
                "INSERT INTO __schema_version (Version, AppliedAt) VALUES (@v, @d)",
                new { v = version, d = DateTime.UtcNow.ToString("o") });
        }
    }

    private static readonly (int Version, string Sql)[] Migrations =
    [
        (1, @"
            CREATE TABLE users (
                Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
                Login                       TEXT    NOT NULL UNIQUE,
                UserPrincipalName           TEXT    NOT NULL DEFAULT '',
                DisplayName                 TEXT    NOT NULL DEFAULT '',
                FirstName                   TEXT    NOT NULL DEFAULT '',
                LastName                    TEXT    NOT NULL DEFAULT '',
                Email                       TEXT    NOT NULL DEFAULT '',
                Phone                       TEXT    NOT NULL DEFAULT '',
                Mobile                      TEXT    NOT NULL DEFAULT '',
                JobTitle                    TEXT    NOT NULL DEFAULT '',
                Department                  TEXT    NOT NULL DEFAULT '',
                Company                     TEXT    NOT NULL DEFAULT '',
                Office                      TEXT    NOT NULL DEFAULT '',
                ManagerDistinguishedName    TEXT    NOT NULL DEFAULT '',
                ManagerDisplayName          TEXT    NOT NULL DEFAULT '',
                DistinguishedName           TEXT    NOT NULL DEFAULT '',
                Description                 TEXT    NOT NULL DEFAULT '',
                Groups                      TEXT    NOT NULL DEFAULT '[]',
                PhotoBase64                 TEXT    NOT NULL DEFAULT '',
                EmployeeId                  TEXT    NOT NULL DEFAULT '',
                IsActive                    INTEGER NOT NULL DEFAULT 1,
                CreatedInAd                 TEXT    NULL,
                ModifiedInAd                TEXT    NULL,
                LastSyncedAt                TEXT    NOT NULL DEFAULT '',
                SearchText                  TEXT    NOT NULL DEFAULT ''
            );
            CREATE INDEX idx_users_email      ON users(Email);
            CREATE INDEX idx_users_displayname ON users(DisplayName);
            CREATE INDEX idx_users_department  ON users(Department);
            CREATE INDEX idx_users_searchtext  ON users(SearchText);"),

        (2, "ALTER TABLE users ADD COLUMN Sid TEXT NOT NULL DEFAULT '';"),

        (3, @"CREATE TABLE favorites (
            Login    TEXT PRIMARY KEY,
            AddedAt  TEXT NOT NULL
        );"),

        (4, @"CREATE TABLE recently_viewed (
            Login    TEXT PRIMARY KEY,
            ViewedAt TEXT NOT NULL
        );")
    ];
}

// Minimal Dapper extension to avoid a using statement clash in this file
file static class SqliteConnectionExtensions
{
    public static void Execute(this SqliteConnection c, string sql, object? param = null)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        if (param != null)
        {
            foreach (var prop in param.GetType().GetProperties())
                cmd.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(param) ?? DBNull.Value);
        }
        if (c.State != System.Data.ConnectionState.Open) c.Open();
        cmd.ExecuteNonQuery();
    }

    public static IEnumerable<T> Query<T>(this SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        if (c.State != System.Data.ConnectionState.Open) c.Open();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return (T)Convert.ChangeType(reader[0], typeof(T));
    }
}
