namespace CompanyDirectory.Shared.Configuration;

public class ApiClientSettings
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = string.Empty;
}
