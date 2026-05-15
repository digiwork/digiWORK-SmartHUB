using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class ChatPage : Page
{
    public ChatViewModel ViewModel { get; }

    public ChatPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ChatViewModel>();
        ViewModel.MessagesScrollRequested += (_, _) => ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            MessagesScrollViewer.ChangeView(null, double.MaxValue, null, disableAnimation: true);
        });
    }

    private void OnMessageInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SendCommand.CanExecute(null))
                ViewModel.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnMessageInputKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
            e.Handled = true;
    }
}
