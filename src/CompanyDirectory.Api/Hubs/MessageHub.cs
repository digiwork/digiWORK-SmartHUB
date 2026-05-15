using CompanyDirectory.Api.Data;
using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CompanyDirectory.Api.Hubs;

[Authorize]
public class MessageHub(
    IMessageRepository repository,
    IConfiguration configuration) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var login = GetLogin();
        var unread = await repository.GetUnreadAsync(login);
        await Clients.Caller.SendAsync("UnreadMessages", unread);
        await base.OnConnectedAsync();
    }

    public async Task BroadcastMessageAsync(MessageDto message)
    {
        var adminGroup = configuration["Authentication:AdminGroup"] ?? string.Empty;
        if (!string.IsNullOrEmpty(adminGroup) && !Context.User!.IsInRole(adminGroup))
            throw new HubException("Brak uprawnień do wysyłania wiadomości.");

        var login = GetLogin();
        message.AuthorLogin        = login;
        message.AuthorDisplayName  = Context.User?.Identity?.Name ?? login;
        message.CreatedAt          = DateTime.UtcNow;

        var id = await repository.CreateAsync(message);
        message.Id = id;

        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public async Task ConfirmReadAsync(int messageId)
    {
        var login = GetLogin();
        await repository.MarkAsReadAsync(messageId, login);
    }

    private string GetLogin()
    {
        var name  = Context.User?.Identity?.Name ?? string.Empty;
        var parts = name.Split('\\');
        return (parts.Length > 1 ? parts[1] : name).ToLowerInvariant();
    }
}
