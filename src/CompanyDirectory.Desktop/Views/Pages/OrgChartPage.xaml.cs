using CompanyDirectory_Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;

namespace CompanyDirectory_Desktop.Views.Pages;

public sealed partial class OrgChartPage : Page
{
    public OrgChartViewModel ViewModel { get; }

    public OrgChartPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OrgChartViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Roots.Count == 0 && !ViewModel.IsLoading)
            _ = ViewModel.LoadCommand.ExecuteAsync(null);
        else if (ViewModel.Roots.Count > 0)
            RebuildTreeViewNodes();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrgChartViewModel.Roots))
            RebuildTreeViewNodes();
    }

    private void RebuildTreeViewNodes()
    {
        OrgTreeView.RootNodes.Clear();
        foreach (var root in ViewModel.Roots)
            OrgTreeView.RootNodes.Add(ToTreeViewNode(root));
    }

    private static TreeViewNode ToTreeViewNode(OrgNode orgNode)
    {
        var tvn = new TreeViewNode { Content = orgNode };
        foreach (var child in orgNode.Children)
            tvn.Children.Add(ToTreeViewNode(child));
        return tvn;
    }

    private async void OnNodeInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
    {
        if (e.InvokedItem is not TreeViewNode { Content: OrgNode node }
            || string.IsNullOrEmpty(node.Login)) return;

        var cache    = App.Services.GetRequiredService<LocalCache.IUserCacheRepository>();
        var user     = await cache.GetByLoginAsync(node.Login);
        if (user is null) return;

        var searchVm = App.Services.GetRequiredService<SearchViewModel>();
        searchVm.NavigateToUser(user);
        App.Services.GetRequiredService<MainWindow>().NavigateTo("search");
    }
}
