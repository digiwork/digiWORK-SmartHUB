using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace CompanyDirectory_Desktop.ViewModels;

public class OrgNode
{
    public string Name     { get; init; } = string.Empty;
    public string Role     { get; init; } = string.Empty;
    public string Login    { get; init; } = string.Empty;
    public bool   IsOuNode { get; init; }

    public Visibility FolderVisibility => IsOuNode ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PersonVisibility => IsOuNode ? Visibility.Collapsed : Visibility.Visible;

    public ObservableCollection<OrgNode> Children { get; } = [];
}