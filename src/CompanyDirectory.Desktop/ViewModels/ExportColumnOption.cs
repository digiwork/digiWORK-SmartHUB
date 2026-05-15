using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory_Desktop.ViewModels;

public class ExportColumnOption
{
    public string Header { get; init; } = string.Empty;
    public Func<UserDirectoryEntryDto, string> Selector { get; init; } = _ => string.Empty;
    public bool IsSelected { get; set; } = true;
}
