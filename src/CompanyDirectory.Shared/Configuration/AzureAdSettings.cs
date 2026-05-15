namespace CompanyDirectory.Shared.Configuration;

public class AzureAdSettings
{
    public const string SectionName = "AzureAd";
    public string TenantId    { get; set; } = "";
    public string ClientId    { get; set; } = "";     // Desktop app client ID
    public string ApiClientId { get; set; } = "";     // API client ID (for scope construction)
}
