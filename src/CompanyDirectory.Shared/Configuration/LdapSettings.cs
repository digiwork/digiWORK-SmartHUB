namespace CompanyDirectory.Shared.Configuration;

public class LdapSettings
{
    public const string SectionName = "Ldap";

    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public bool UseSsl { get; set; } = true;
    public string BaseDn { get; set; } = string.Empty;
    public string SearchBase { get; set; } = string.Empty;
    public bool UseDefaultCredentials { get; set; } = true;
    public string ServiceAccountUser { get; set; } = string.Empty;
    public string ServiceAccountPassword { get; set; } = string.Empty;
    public int PageSize { get; set; } = 500;
    public List<string> AllowedAttributes { get; set; } =
    [
        "sAMAccountName", "userPrincipalName", "displayName",
        "givenName", "sn", "mail", "telephoneNumber", "mobile",
        "title", "department", "company", "physicalDeliveryOfficeName",
        "manager", "distinguishedName", "memberOf", "description",
        "thumbnailPhoto", "whenCreated", "whenChanged", "employeeID",
        "userAccountControl"
    ];
}
