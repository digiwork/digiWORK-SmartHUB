using CompanyDirectory.Infrastructure.Ldap;
using CompanyDirectory.Shared.Dtos;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.Json;

namespace CompanyDirectory.Api.Mapping;

[SupportedOSPlatform("windows")]
public static class LdapToUserDtoMapper
{
    public static UserDirectoryEntryDto Map(LdapUserRawEntry entry)
    {
        var uac = ParseInt(entry.GetString("userAccountControl"));
        var isActive = uac.HasValue && (uac.Value & 2) == 0;

        var photoBytes = entry.GetBytes("thumbnailPhoto");

        return new UserDirectoryEntryDto
        {
            Login                    = entry.GetString("sAMAccountName") ?? string.Empty,
            UserPrincipalName        = entry.GetString("userPrincipalName") ?? string.Empty,
            DisplayName              = entry.GetString("displayName") ?? string.Empty,
            FirstName                = entry.GetString("givenName") ?? string.Empty,
            LastName                 = entry.GetString("sn") ?? string.Empty,
            Email                    = entry.GetString("mail") ?? string.Empty,
            Phone                    = entry.GetString("telephoneNumber") ?? string.Empty,
            Mobile                   = entry.GetString("mobile") ?? string.Empty,
            JobTitle                 = entry.GetString("title") ?? string.Empty,
            Department               = entry.GetString("department") ?? string.Empty,
            Company                  = entry.GetString("company") ?? string.Empty,
            Office                   = entry.GetString("physicalDeliveryOfficeName") ?? string.Empty,
            ManagerDistinguishedName = entry.GetString("manager") ?? string.Empty,
            ManagerDisplayName       = string.Empty, // resolved separately
            DistinguishedName        = entry.DistinguishedName,
            Description              = entry.GetString("description") ?? string.Empty,
            Groups                   = SerializeGroups(entry.GetStringArray("memberOf")),
            PhotoBase64              = photoBytes is { Length: > 0 }
                                           ? Convert.ToBase64String(photoBytes)
                                           : string.Empty,
            EmployeeId               = entry.GetString("employeeID") ?? string.Empty,
            Sid                      = ConvertSid(entry.GetBytes("objectSid")),
            IsActive                 = isActive,
            CreatedInAd              = ParseLdapDateTime(entry.GetString("whenCreated")),
            ModifiedInAd             = ParseLdapDateTime(entry.GetString("whenChanged")),
            LastSyncedAt             = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Parses the LDAP generalized time format: yyyyMMddHHmmss.0Z
    /// </summary>
    private static DateTime? ParseLdapDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        // Try generalized time (yyyyMMddHHmmss.0Z) then ISO 8601
        if (DateTime.TryParseExact(
                value,
                ["yyyyMMddHHmmss.0Z", "yyyyMMddHHmmss.fZ", "yyyyMMddHHmmssZ"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var result))
        {
            return result;
        }

        return null;
    }

    private static string ConvertSid(byte[]? sidBytes)
    {
        if (sidBytes is null or { Length: 0 }) return string.Empty;
        try   { return new SecurityIdentifier(sidBytes, 0).Value; }
        catch { return string.Empty; }
    }

    private static string SerializeGroups(string[] groups)
        => groups.Length == 0 ? "[]" : JsonSerializer.Serialize(groups);

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var i) ? i : null;
}
