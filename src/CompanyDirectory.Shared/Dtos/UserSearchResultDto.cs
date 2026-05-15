namespace CompanyDirectory.Shared.Dtos;

public class UserSearchResultDto
{
    public List<UserDirectoryEntryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
