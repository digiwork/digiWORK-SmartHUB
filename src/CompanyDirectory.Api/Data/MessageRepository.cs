using CompanyDirectory.Shared.Dtos;
using Dapper;

namespace CompanyDirectory.Api.Data;

public class MessageRepository(MessageDbInitializer db) : IMessageRepository
{
    public async Task<int> CreateAsync(MessageDto message)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO messages (Title, Body, AuthorLogin, AuthorDisplayName, CreatedAt, RequiresConfirmation, IsActive)
            VALUES (@Title, @Body, @AuthorLogin, @AuthorDisplayName, @CreatedAt, @RequiresConfirmation, 1);
            SELECT last_insert_rowid();",
            new
            {
                message.Title,
                message.Body,
                message.AuthorLogin,
                message.AuthorDisplayName,
                CreatedAt            = message.CreatedAt.ToString("o"),
                RequiresConfirmation = message.RequiresConfirmation ? 1 : 0,
            });
    }

    public async Task<List<MessageDto>> GetUnreadAsync(string userLogin)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<MessageRow>(@"
            SELECT m.*
            FROM   messages m
            WHERE  m.IsActive = 1
              AND  NOT EXISTS (
                    SELECT 1 FROM message_reads r
                    WHERE r.MessageId = m.Id AND r.UserLogin = @userLogin)
            ORDER BY m.CreatedAt DESC",
            new { userLogin });
        return [.. rows.Select(MapRow)];
    }

    public async Task<List<MessageDto>> GetReadAsync(string userLogin)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<MessageRow>(@"
            SELECT m.*
            FROM   messages m
            JOIN   message_reads r ON r.MessageId = m.Id AND r.UserLogin = @userLogin
            ORDER BY r.ReadAt DESC",
            new { userLogin });
        return [.. rows.Select(MapRow)];
    }

    public async Task<List<MessageDto>> GetAllAsync()
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<MessageRow>(@"
            SELECT * FROM messages
            ORDER BY CreatedAt DESC");
        return [.. rows.Select(MapRow)];
    }

    public async Task MarkAsReadAsync(int messageId, string userLogin)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT OR IGNORE INTO message_reads (MessageId, UserLogin, ReadAt)
            VALUES (@messageId, @userLogin, @readAt)",
            new { messageId, userLogin, readAt = DateTime.UtcNow.ToString("o") });
    }

    private static MessageDto MapRow(MessageRow r) => new()
    {
        Id                   = r.Id,
        Title                = r.Title,
        Body                 = r.Body,
        AuthorLogin          = r.AuthorLogin,
        AuthorDisplayName    = r.AuthorDisplayName,
        CreatedAt            = DateTime.Parse(r.CreatedAt, null,
                                   System.Globalization.DateTimeStyles.RoundtripKind),
        RequiresConfirmation = r.RequiresConfirmation == 1,
    };

    private class MessageRow
    {
        public int    Id                   { get; set; }
        public string Title                { get; set; } = "";
        public string Body                 { get; set; } = "";
        public string AuthorLogin          { get; set; } = "";
        public string AuthorDisplayName    { get; set; } = "";
        public string CreatedAt            { get; set; } = "";
        public int    RequiresConfirmation { get; set; }
    }
}
