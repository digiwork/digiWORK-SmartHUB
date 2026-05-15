namespace CompanyDirectory.Shared.Configuration;

public class LocalCacheSettings
{
    public const string SectionName = "LocalCache";

    public string DatabasePath { get; set; } = @"%LocalAppData%\CompanyDirectory\directory.db";
}
