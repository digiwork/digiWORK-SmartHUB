using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using CompanyDirectory_Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.Security.Principal;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService            _chatService;
    private readonly IUserCacheRepository   _userCache;
    private readonly ILogger<ChatViewModel> _logger;

    private string _myLogin        = string.Empty;
    private string _myDisplayName  = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChatPanelVisibility))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial ConversationSummaryDto? SelectedConversation { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyIndicatorVisibility))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public Visibility BusyIndicatorVisibility =>
        IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<ConversationSummaryDto> Conversations { get; } = [];
    public ObservableCollection<ChatMessageEntry>        Messages      { get; } = [];

    public Visibility ChatPanelVisibility  =>
        SelectedConversation is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility =>
        SelectedConversation is null ? Visibility.Visible : Visibility.Collapsed;

    public event EventHandler? MessagesScrollRequested;

    public ChatViewModel(ChatService chatService, IUserCacheRepository userCache, ILogger<ChatViewModel> logger)
    {
        _chatService = chatService;
        _userCache   = userCache;
        _logger      = logger;

        ResolveCurrentUser();

        _chatService.ConversationsLoaded += OnConversationsLoaded;
        _chatService.MessageReceived     += OnMessageReceived;
        _chatService.MessagesRead        += OnMessagesRead;
    }

    private void ResolveCurrentUser()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var name     = identity.Name ?? string.Empty;
            _myLogin = name.Contains('\\') ? name.Split('\\')[1].ToLowerInvariant() : name.ToLowerInvariant();

            var me = _userCache.GetByLoginAsync(_myLogin).GetAwaiter().GetResult();
            _myDisplayName = me?.DisplayName ?? _myLogin;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve current user identity");
        }
    }

    partial void OnSelectedConversationChanged(ConversationSummaryDto? value)
    {
        if (value is null) return;
        _ = LoadHistoryAsync(value.OtherLogin);
    }

    private void OnConversationsLoaded(List<ConversationSummaryDto> convs)
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            Conversations.Clear();
            foreach (var c in convs) Conversations.Add(c);
        });
    }

    private void OnMessageReceived(ChatMessageDto msg)
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            var isCurrentConversation =
                SelectedConversation is not null &&
                (msg.FromLogin == SelectedConversation.OtherLogin ||
                 msg.ToLogin   == SelectedConversation.OtherLogin);

            if (isCurrentConversation)
            {
                Messages.Add(new ChatMessageEntry
                {
                    Message = msg,
                    IsOwn   = msg.FromLogin == _myLogin,
                });
                MessagesScrollRequested?.Invoke(this, EventArgs.Empty);

                if (msg.FromLogin != _myLogin)
                    _ = _chatService.MarkAsReadAsync(msg.FromLogin);
            }

            // Update conversation list
            _ = RefreshConversationsAsync();
        });
    }

    private void OnMessagesRead(string otherLogin)
    {
        // Could update read indicators if desired
        _ = RefreshConversationsAsync();
    }

    private async Task RefreshConversationsAsync()
    {
        try
        {
            var convs = await _chatService.GetConversationsAsync();
            App.DispatcherQueue.TryEnqueue(() =>
            {
                Conversations.Clear();
                foreach (var c in convs) Conversations.Add(c);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh conversations");
        }
    }

    private async Task LoadHistoryAsync(string otherLogin)
    {
        IsBusy = true;
        try
        {
            var history = await _chatService.GetHistoryAsync(otherLogin);
            App.DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Clear();
                foreach (var msg in history)
                    Messages.Add(new ChatMessageEntry { Message = msg, IsOwn = msg.FromLogin == _myLogin });
                MessagesScrollRequested?.Invoke(this, EventArgs.Empty);
            });
            await _chatService.MarkAsReadAsync(otherLogin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load chat history with {Login}", otherLogin);
            StatusMessage = "Nie udało się załadować historii";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var conv = SelectedConversation;
        if (conv is null || string.IsNullOrWhiteSpace(MessageText)) return;

        var text = MessageText;
        MessageText = string.Empty;

        try
        {
            await _chatService.SendMessageAsync(
                conv.OtherLogin,
                conv.OtherDisplayName,
                text,
                _myDisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat message");
            MessageText   = text; // restore
            StatusMessage = "Błąd wysyłania wiadomości";
        }
    }

    private bool CanSend() =>
        SelectedConversation is not null &&
        !string.IsNullOrWhiteSpace(MessageText);

    public async Task OpenConversationAsync(string otherLogin, string otherDisplayName)
    {
        var existing = Conversations.FirstOrDefault(c => c.OtherLogin == otherLogin);
        if (existing is not null)
        {
            SelectedConversation = existing;
            return;
        }

        // Create a placeholder conversation
        var placeholder = new ConversationSummaryDto
        {
            OtherLogin        = otherLogin,
            OtherDisplayName  = otherDisplayName,
            LastMessagePreview = string.Empty,
            LastMessageAt     = DateTime.MinValue,
        };
        Conversations.Insert(0, placeholder);
        SelectedConversation = placeholder;
    }
}
