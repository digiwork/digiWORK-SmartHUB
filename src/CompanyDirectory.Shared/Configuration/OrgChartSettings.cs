namespace CompanyDirectory.Shared.Configuration;

public class OrgChartSettings
{
    public const string SectionName = "OrgChart";

    /// <summary>
    /// Only users with this exact Company value are shown in the org chart.
    /// Leave empty to show all active users.
    /// </summary>
    public string CompanyFilter { get; set; } = string.Empty;
}
