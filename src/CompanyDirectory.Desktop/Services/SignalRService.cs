using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyDirectory_Desktop.Services;

public class SignalRService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<SignalRService> _logger;

    public event Action<MessageDto>?       MessageReceived;
    public event Action<List<MessageDto>>? UnreadMessagesReceived;

    public HubConnectionState State => _connection.State;

    public SignalRService(IOptions<ApiClientSettings> apiOptions, AuthService auth, ILogger<SignalRService> logger)
    {
        _logger = logger;
        var baseUrl = apiOptions.Value.BaseUrl.TrimEnd('/');

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/messages", options =>
            {
                options.AccessTokenProvider = async () => await auth.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<MessageDto>("ReceiveMessage", msg =>
        {
            _logger.LogInformation("SignalR message received: {Title}", msg.Title);
            MessageReceived?.Invoke(msg);
        });

        _connection.On<List<MessageDto>>("UnreadMessages", msgs =>
        {
            _logger.LogInformation("SignalR: {Count} unread messages loaded", msgs.Count);
            UnreadMessagesReceived?.Invoke(msgs);
        });

        _connection.Reconnected += _ =>
        {
            _logger.LogInformation("SignalR reconnected");
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("SignalR connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed to connect — automatic reconnect will retry");
        }
    }

    public async Task StopAsync()
    {
        try { await _connection.StopAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR stop error"); }
    }

    public async Task MarkAsReadAsync(int messageId)
    {
        if (_connection.State != HubConnectionState.Connected) return;
        await _connection.InvokeAsync("ConfirmReadAsync", messageId);
    }

    public async Task BroadcastMessageAsync(MessageDto message)
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Brak połączenia z serwerem.");
        await _connection.InvokeAsync("BroadcastMessageAsync", message);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
