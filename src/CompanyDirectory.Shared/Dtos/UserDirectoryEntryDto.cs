namespace CompanyDirectory.Shared.Dtos;

public class UserDirectoryEntryDto
{
    public int Id { get; set; }

    public string Login { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;

    public string ManagerDistinguishedName { get; set; } = string.Empty;
    public string ManagerDisplayName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>JSON array of group distinguished names from memberOf.</summary>
    public string Groups { get; set; } = "[]";

    /// <summary>Base64-encoded JPEG from thumbnailPhoto AD attribute.</summary>
    public string PhotoBase64 { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string Sid { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? CreatedInAd { get; set; }
    public DateTime? ModifiedInAd { get; set; }
    public DateTime LastSyncedAt { get; set; }
}
