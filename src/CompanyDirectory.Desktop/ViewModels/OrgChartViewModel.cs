using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;

namespace CompanyDirectory_Desktop.ViewModels;

public partial class OrgChartViewModel : ObservableObject
{
    private readonly IUserCacheRepository _repository;
    private readonly string               _companyFilter;

    [ObservableProperty]
    public partial ObservableCollection<OrgNode> Roots { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public IAsyncRelayCommand LoadCommand { get; }

    public OrgChartViewModel(IUserCacheRepository repository, IOptions<OrgChartSettings> options)
    {
        _repository    = repository;
        _companyFilter = options.Value.CompanyFilter;
        LoadCommand    = new AsyncRelayCommand(LoadAsync);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            var users = await _repository.GetAllActiveAsync(_companyFilter);
            Roots = BuildTree(users);
            StatusMessage = $"{users.Count} aktywnych pracowników";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static ObservableCollection<OrgNode> BuildTree(List<UserDirectoryEntryDto> users)
    {
        var roots    = new ObservableCollection<OrgNode>();
        var ouCache  = new Dictionary<string, OrgNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in users.OrderBy(u => u.DisplayName))
        {
            var ous    = ParseOuPath(user.DistinguishedName); // root → leaf, e.g. ["AGROAS","Pracownicy","IT"]
            var pathSb = new System.Text.StringBuilder();
            OrgNode? parent = null;

            for (int i = 0; i < ous.Length; i++)
            {
                if (i > 0) pathSb.Append('\x00');
                pathSb.Append(ous[i]);
                var path = pathSb.ToString();

                if (!ouCache.TryGetValue(path, out var ouNode))
                {
                    ouNode = new OrgNode { Name = ous[i], IsOuNode = true };
                    ouCache[path] = ouNode;

                    if (parent is null) roots.Add(ouNode);
                    else                parent.Children.Add(ouNode);
                }
                parent = ouNode;
            }

            var userNode = new OrgNode { Name = user.DisplayName, Role = user.JobTitle, Login = user.Login };

            if (parent is null) roots.Add(userNode);
            else                parent.Children.Add(userNode);
        }

        return roots;
    }

    private static string[] ParseOuPath(string? dn)
    {
        if (string.IsNullOrEmpty(dn)) return [];

        // DN: CN=Jan,OU=IT,OU=Pracownicy,OU=AGROAS,DC=domain,DC=local
        // Extract OU parts then reverse so root comes first
        return dn.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            .Select(part => part[3..])
            .Reverse()
            .ToArray();
    }
}
