using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.ViewModels;
using System.Text;
using Windows.Storage;

namespace CompanyDirectory_Desktop.Services;

public class ExportService
{
    public async Task ExportToCsvAsync(
        IEnumerable<UserDirectoryEntryDto> users,
        IEnumerable<ExportColumnOption>    columns,
        StorageFile                        file)
    {
        var cols = columns.ToList();
        var sb   = new StringBuilder();

        sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.Header))));

        foreach (var user in users)
            sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.Selector(user)))));

        await FileIO.WriteTextAsync(file, sb.ToString(),
            Windows.Storage.Streams.UnicodeEncoding.Utf8);
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
