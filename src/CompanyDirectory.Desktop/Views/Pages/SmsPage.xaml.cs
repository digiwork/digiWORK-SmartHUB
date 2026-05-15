using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class SmsPage : Page
{
    public SmsViewModel ViewModel { get; }

    public SmsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SmsViewModel>();
    }

    private void OnSearchResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is UserDirectoryEntryDto user)
            ViewModel.AddRecipient(user);
    }

    private void OnRemoveRecipientClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SmsRecipientEntry entry })
            ViewModel.RemoveRecipient(entry);
    }
}
