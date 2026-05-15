using Microsoft.Data.Sqlite;

namespace CompanyDirectory.Api.Data;

public class MessageDbInitializer(IConfiguration configuration, ILogger<MessageDbInitializer> logger)
{
    private readonly string _dbPath = Environment.ExpandEnvironmentVariables(
        configuration["Messages:DatabasePath"] ?? "messages.db");

    public string DatabasePath => _dbPath;

    public void Initialize()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = OpenConnection();
        conn.Open();

        conn.Execute(@"PRAGMA journal_mode=WAL;");
        conn.Execute(@"CREATE TABLE IF NOT EXISTS __schema_version (
            Version   INTEGER PRIMARY KEY,
            AppliedAt TEXT NOT NULL);");

        var applied = new HashSet<int>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Version FROM __schema_version";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) applied.Add(reader.GetInt32(0));
        }

        foreach (var (version, sql) in Migrations)
        {
            if (applied.Contains(version)) continue;
            logger.LogInformation("Applying messages DB migration v{Version}", version);
            conn.Execute(sql);
            conn.Execute(
                "INSERT INTO __schema_version (Version, AppliedAt) VALUES (@v, @d)",
                ("v", version), ("d", DateTime.UtcNow.ToString("o")));
        }

        logger.LogInformation("Messages database initialized at {Path}", _dbPath);
    }

    public SqliteConnection OpenConnection()
        => new($"Data Source={_dbPath};Mode=ReadWriteCreate;Foreign Keys=True");

    private static readonly (int Version, string Sql)[] Migrations =
    [
        (1, @"
            CREATE TABLE messages (
                Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                Title                TEXT    NOT NULL DEFAULT '',
                Body                 TEXT    NOT NULL DEFAULT '',
                AuthorLogin          TEXT    NOT NULL DEFAULT '',
                AuthorDisplayName    TEXT    NOT NULL DEFAULT '',
                CreatedAt            TEXT    NOT NULL,
                RequiresConfirmation INTEGER NOT NULL DEFAULT 1,
                IsActive             INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE message_reads (
                MessageId  INTEGER NOT NULL REFERENCES messages(Id),
                UserLogin  TEXT    NOT NULL,
                ReadAt     TEXT    NOT NULL,
                PRIMARY KEY (MessageId, UserLogin)
            );"),
        (2, @"
            CREATE TABLE chat_messages (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                FromLogin       TEXT    NOT NULL,
                FromDisplayName TEXT    NOT NULL DEFAULT '',
                ToLogin         TEXT    NOT NULL,
                ToDisplayName   TEXT    NOT NULL DEFAULT '',
                Body            TEXT    NOT NULL,
                SentAt          TEXT    NOT NULL,
                IsRead          INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX idx_chat_from_to ON chat_messages(FromLogin, ToLogin);
            CREATE INDEX idx_chat_to_from ON chat_messages(ToLogin, FromLogin);"),
    ];
}

file static class SqliteConnectionExtensions
{
    public static void Execute(this SqliteConnection c, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue("@" + name, value ?? DBNull.Value);
        if (c.State != System.Data.ConnectionState.Open) c.Open();
        cmd.ExecuteNonQuery();
    }
}
