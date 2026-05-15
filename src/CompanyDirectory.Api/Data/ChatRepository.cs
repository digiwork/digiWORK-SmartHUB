using CompanyDirectory.Shared.Dtos;
using Dapper;

namespace CompanyDirectory.Api.Data;

public class ChatRepository(MessageDbInitializer db) : IChatRepository
{
    public async Task<ChatMessageDto> SendAsync(ChatMessageDto message)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();

        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO chat_messages
                (FromLogin, FromDisplayName, ToLogin, ToDisplayName, Body, SentAt, IsRead)
            VALUES
                (@FromLogin, @FromDisplayName, @ToLogin, @ToDisplayName, @Body, @SentAt, 0);
            SELECT last_insert_rowid();",
            new
            {
                message.FromLogin,
                message.FromDisplayName,
                message.ToLogin,
                message.ToDisplayName,
                message.Body,
                SentAt = message.SentAt.ToString("o"),
            });

        message.Id = id;
        return message;
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(string loginA, string loginB, int limit = 50)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();

        var rows = await conn.QueryAsync<ChatRow>(@"
            SELECT * FROM chat_messages
            WHERE (FromLogin = @loginA AND ToLogin = @loginB)
               OR (FromLogin = @loginB AND ToLogin = @loginA)
            ORDER BY SentAt ASC
            LIMIT @limit",
            new { loginA, loginB, limit });

        return [.. rows.Select(MapRow)];
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync(string myLogin)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();

        var rows = await conn.QueryAsync<ChatRow>(@"
            SELECT * FROM chat_messages
            WHERE FromLogin = @myLogin OR ToLogin = @myLogin
            ORDER BY SentAt DESC",
            new { myLogin });

        return rows
            .GroupBy(r => r.FromLogin == myLogin ? r.ToLogin : r.FromLogin)
            .Select(g =>
            {
                var last = g.First();
                var isOwn = last.FromLogin == myLogin;
                var otherLogin       = isOwn ? last.ToLogin       : last.FromLogin;
                var otherDisplayName = isOwn ? last.ToDisplayName : last.FromDisplayName;
                var preview = last.Body.Length > 60 ? last.Body[..60] + "…" : last.Body;
                return new ConversationSummaryDto
                {
                    OtherLogin         = otherLogin,
                    OtherDisplayName   = otherDisplayName,
                    LastMessagePreview = preview,
                    LastMessageAt      = DateTime.Parse(last.SentAt, null,
                                            System.Globalization.DateTimeStyles.RoundtripKind),
                    UnreadCount        = g.Count(r => r.ToLogin == myLogin && r.IsRead == 0),
                    LastMessageIsOwn   = isOwn,
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    public async Task MarkAsReadAsync(string fromLogin, string toLogin)
    {
        using var conn = db.OpenConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
            UPDATE chat_messages SET IsRead = 1
            WHERE FromLogin = @fromLogin AND ToLogin = @toLogin AND IsRead = 0",
            new { fromLogin, toLogin });
    }

    private static ChatMessageDto MapRow(ChatRow r) => new()
    {
        Id              = r.Id,
        FromLogin       = r.FromLogin,
        FromDisplayName = r.FromDisplayName,
        ToLogin         = r.ToLogin,
        ToDisplayName   = r.ToDisplayName,
        Body            = r.Body,
        SentAt          = DateTime.Parse(r.SentAt, null,
                              System.Globalization.DateTimeStyles.RoundtripKind),
        IsRead          = r.IsRead == 1,
    };

    private class ChatRow
    {
        public int    Id              { get; set; }
        public string FromLogin       { get; set; } = "";
        public string FromDisplayName { get; set; } = "";
        public string ToLogin         { get; set; } = "";
        public string ToDisplayName   { get; set; } = "";
        public string Body            { get; set; } = "";
        public string SentAt          { get; set; } = "";
        public int    IsRead          { get; set; }
    }
}
