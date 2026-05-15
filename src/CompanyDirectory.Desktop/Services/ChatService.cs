using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyDirectory_Desktop.Services;

public class ChatService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<ChatService> _logger;

    public event Action<ChatMessageDto>?          MessageReceived;
    public event Action<List<ConversationSummaryDto>>? ConversationsLoaded;
    public event Action<string>?                  MessagesRead; // otherLogin

    public HubConnectionState State => _connection.State;

    public ChatService(IOptions<ApiClientSettings> apiOptions, AuthService auth, ILogger<ChatService> logger)
    {
        _logger = logger;
        var baseUrl = apiOptions.Value.BaseUrl.TrimEnd('/');

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = async () => await auth.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<ChatMessageDto>("ReceiveMessage", msg =>
        {
            _logger.LogDebug("Chat message received from {From}", msg.FromLogin);
            MessageReceived?.Invoke(msg);
        });

        _connection.On<List<ConversationSummaryDto>>("ConversationsLoaded", convs =>
        {
            _logger.LogDebug("Conversations loaded: {Count}", convs.Count);
            ConversationsLoaded?.Invoke(convs);
        });

        _connection.On<string>("MessagesRead", otherLogin =>
        {
            MessagesRead?.Invoke(otherLogin);
        });

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("Chat SignalR reconnected");
            // Re-load conversations after reconnect
            try
            {
                var convs = await _connection.InvokeAsync<List<ConversationSummaryDto>>("GetConversationsAsync");
                ConversationsLoaded?.Invoke(convs);
            }
            catch { }
        };
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("Chat SignalR connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat SignalR failed to connect — will auto-retry");
        }
    }

    public async Task StopAsync()
    {
        try { await _connection.StopAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Chat SignalR stop error"); }
    }

    public async Task SendMessageAsync(string toLogin, string toDisplayName, string body, string fromDisplayName)
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Brak połączenia z serwerem.");
        await _connection.InvokeAsync("SendMessageAsync", toLogin, toDisplayName, body, fromDisplayName);
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(string otherLogin, int limit = 50)
    {
        if (_connection.State != HubConnectionState.Connected) return [];
        return await _connection.InvokeAsync<List<ChatMessageDto>>("GetHistoryAsync", otherLogin, limit);
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync()
    {
        if (_connection.State != HubConnectionState.Connected) return [];
        return await _connection.InvokeAsync<List<ConversationSummaryDto>>("GetConversationsAsync");
    }

    public async Task MarkAsReadAsync(string fromLogin)
    {
        if (_connection.State != HubConnectionState.Connected) return;
        await _connection.InvokeAsync("MarkAsReadAsync", fromLogin);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
