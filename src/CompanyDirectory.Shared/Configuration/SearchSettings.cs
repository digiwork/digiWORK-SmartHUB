namespace CompanyDirectory.Shared.Configuration;

public class SearchSettings
{
    public const string SectionName = "Search";

    public int MinimumQueryLength { get; set; } = 2;
    public int MaxResults { get; set; } = 50;
}
