using CompanyDirectory.Api.Data;
using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CompanyDirectory.Api.Hubs;

[Authorize]
public class ChatHub(IChatRepository repository) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var login = GetLogin();
        var conversations = await repository.GetConversationsAsync(login);
        await Clients.Caller.SendAsync("ConversationsLoaded", conversations);
        await base.OnConnectedAsync();
    }

    public async Task SendMessageAsync(string toLogin, string toDisplayName, string body, string fromDisplayName)
    {
        if (string.IsNullOrWhiteSpace(body)) return;

        var fromLogin = GetLogin();
        var message = new ChatMessageDto
        {
            FromLogin       = fromLogin,
            FromDisplayName = fromDisplayName,
            ToLogin         = toLogin,
            ToDisplayName   = toDisplayName,
            Body            = body,
            SentAt          = DateTime.UtcNow,
        };

        var saved = await repository.SendAsync(message);

        // Deliver to recipient and to all other connections of the sender
        await Clients.User(toLogin).SendAsync("ReceiveMessage", saved);
        await Clients.User(fromLogin).SendAsync("ReceiveMessage", saved);
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(string otherLogin, int limit = 50)
    {
        var myLogin = GetLogin();
        return await repository.GetHistoryAsync(myLogin, otherLogin, limit);
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync()
    {
        var myLogin = GetLogin();
        return await repository.GetConversationsAsync(myLogin);
    }

    public async Task MarkAsReadAsync(string fromLogin)
    {
        var myLogin = GetLogin();
        await repository.MarkAsReadAsync(fromLogin, myLogin);
        await Clients.User(fromLogin).SendAsync("MessagesRead", myLogin);
    }

    private string GetLogin()
    {
        var name  = Context.User?.Identity?.Name ?? string.Empty;
        var parts = name.Split('\\');
        return (parts.Length > 1 ? parts[1] : name).ToLowerInvariant();
    }
}
