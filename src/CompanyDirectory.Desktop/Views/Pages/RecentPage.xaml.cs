using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class RecentPage : Page
{
    public RecentViewModel ViewModel { get; }

    public RecentPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<RecentViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnUserItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not UserDirectoryEntryDto user) return;
        var searchVm = App.Services.GetRequiredService<SearchViewModel>();
        searchVm.NavigateToUser(user);
        App.Services.GetRequiredService<MainWindow>().NavigateTo("search");
    }
}
