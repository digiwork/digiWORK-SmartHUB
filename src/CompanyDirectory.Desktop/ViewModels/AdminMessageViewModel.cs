using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.Services;
using Microsoft.Extensions.Logging;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class AdminMessageViewModel(
    SignalRService signalR,
    ILogger<AdminMessageViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Body { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RequiresConfirmation { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        IsSending     = true;
        StatusMessage = string.Empty;
        try
        {
            await signalR.BroadcastMessageAsync(new MessageDto
            {
                Title                = Title.Trim(),
                Body                 = Body.Trim(),
                RequiresConfirmation = RequiresConfirmation,
            });
            StatusMessage = "Wiadomość wysłana pomyślnie.";
            Title = string.Empty;
            Body  = string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send admin message");
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend() =>
        !IsSending &&
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(Body);
}
