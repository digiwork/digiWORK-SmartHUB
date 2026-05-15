using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class InboxPage : Page
{
    public InboxViewModel InboxVm { get; }
    public SentMessagesViewModel SentVm { get; }

    public InboxPage()
    {
        InitializeComponent();
        InboxVm = App.Services.GetRequiredService<InboxViewModel>();
        SentVm  = App.Services.GetRequiredService<SentMessagesViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = InboxVm.LoadCommand.ExecuteAsync(null);
        _ = SentVm.LoadCommand.ExecuteAsync(null);
    }
}
