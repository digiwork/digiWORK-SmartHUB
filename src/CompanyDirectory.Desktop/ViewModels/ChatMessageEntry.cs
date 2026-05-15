using CompanyDirectory.Shared.Dtos;
using Microsoft.UI.Xaml;

namespace CompanyDirectory_Desktop.ViewModels;

public class ChatMessageEntry
{
    public ChatMessageDto Message { get; init; } = null!;
    public bool           IsOwn  { get; init; }

    public Visibility OwnVisibility   => IsOwn ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OtherVisibility => IsOwn ? Visibility.Collapsed : Visibility.Visible;
    public string     TimeDisplay     => Message.SentAt.ToLocalTime().ToString("HH:mm");
}
