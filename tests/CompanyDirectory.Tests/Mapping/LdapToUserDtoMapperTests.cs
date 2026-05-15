using CompanyDirectory.Api.Mapping;
using CompanyDirectory.Infrastructure.Ldap;
using System.Text.Json;

namespace CompanyDirectory.Tests.Mapping;

public class LdapToUserDtoMapperTests
{
    private static LdapUserRawEntry BuildEntry(Dictionary<string, object?> attrs,
        string dn = "CN=Test User,OU=Users,DC=firma,DC=local")
        => new() { DistinguishedName = dn, Attributes = new(attrs, StringComparer.OrdinalIgnoreCase) };

    // ── Field mapping ────────────────────────────────────────────────────────

    [Fact]
    public void Map_BasicFields_MappedCorrectly()
    {
        var entry = BuildEntry(new()
        {
            ["sAMAccountName"]           = "jan.kowalski",
            ["userPrincipalName"]        = "jan.kowalski@firma.local",
            ["displayName"]              = "Jan Kowalski",
            ["givenName"]                = "Jan",
            ["sn"]                       = "Kowalski",
            ["mail"]                     = "j.kowalski@firma.pl",
            ["telephoneNumber"]          = "+48 22 100 0001",
            ["mobile"]                   = "+48 600 000 001",
            ["title"]                    = "Programista",
            ["department"]               = "IT",
            ["company"]                  = "Firma Sp. z o.o.",
            ["physicalDeliveryOfficeName"] = "Warszawa",
            ["manager"]                  = "CN=Anna Kowalczyk,OU=Users,DC=firma,DC=local",
            ["description"]              = "Opis",
            ["employeeID"]               = "EMP001",
            ["userAccountControl"]       = "512",
        });

        var dto = LdapToUserDtoMapper.Map(entry);

        Assert.Equal("jan.kowalski",               dto.Login);
        Assert.Equal("jan.kowalski@firma.local",   dto.UserPrincipalName);
        Assert.Equal("Jan Kowalski",               dto.DisplayName);
        Assert.Equal("Jan",                        dto.FirstName);
        Assert.Equal("Kowalski",                   dto.LastName);
        Assert.Equal("j.kowalski@firma.pl",        dto.Email);
        Assert.Equal("+48 22 100 0001",            dto.Phone);
        Assert.Equal("+48 600 000 001",            dto.Mobile);
        Assert.Equal("Programista",                dto.JobTitle);
        Assert.Equal("IT",                         dto.Department);
        Assert.Equal("Firma Sp. z o.o.",           dto.Company);
        Assert.Equal("Warszawa",                   dto.Office);
        Assert.Equal("CN=Anna Kowalczyk,OU=Users,DC=firma,DC=local", dto.ManagerDistinguishedName);
        Assert.Equal("Opis",                       dto.Description);
        Assert.Equal("EMP001",                     dto.EmployeeId);
        Assert.Equal(entry.DistinguishedName,      dto.DistinguishedName);
    }

    [Fact]
    public void Map_ManagerDisplayName_IsAlwaysEmpty()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new() { ["userAccountControl"] = "512" }));
        Assert.Equal(string.Empty, dto.ManagerDisplayName);
    }

    [Fact]
    public void Map_MissingAttributes_ResultInEmptyStrings()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));

        Assert.Equal(string.Empty, dto.Login);
        Assert.Equal(string.Empty, dto.DisplayName);
        Assert.Equal(string.Empty, dto.Email);
        Assert.Equal(string.Empty, dto.Department);
    }

    // ── IsActive (userAccountControl bit 2) ──────────────────────────────────

    [Theory]
    [InlineData("512",  true)]   // normal user account
    [InlineData("514",  false)]  // 512 + 2 (disabled)
    [InlineData("66048",true)]   // password not required, not disabled
    [InlineData("66050",false)]  // password not required + disabled
    public void Map_IsActive_DependsOnDisabledBit(string uac, bool expectedActive)
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new() { ["userAccountControl"] = uac }));
        Assert.Equal(expectedActive, dto.IsActive);
    }

    [Fact]
    public void Map_MissingUserAccountControl_IsActiveFalse()
    {
        // Missing UAC → ParseInt returns null → isActive = null.HasValue is false → IsActive = false
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));
        Assert.False(dto.IsActive);
    }

    // ── Photo ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_ThumbnailPhoto_EncodedAsBase64()
    {
        var photoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01 }; // JPEG header fragment
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new() { ["thumbnailPhoto"] = photoBytes }));
        Assert.Equal(Convert.ToBase64String(photoBytes), dto.PhotoBase64);
    }

    [Fact]
    public void Map_NoPhoto_PhotoBase64IsEmpty()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));
        Assert.Equal(string.Empty, dto.PhotoBase64);
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    [Fact]
    public void Map_MemberOf_SerializedAsJsonArray()
    {
        var groups = new[] { "CN=IT,OU=Groups,DC=firma,DC=local", "CN=Dev,OU=Groups,DC=firma,DC=local" };
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new() { ["memberOf"] = groups }));

        var parsed = JsonSerializer.Deserialize<string[]>(dto.Groups);
        Assert.Equal(groups, parsed);
    }

    [Fact]
    public void Map_NoMemberOf_GroupsIsEmptyJsonArray()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));
        Assert.Equal("[]", dto.Groups);
    }

    // ── Date parsing ─────────────────────────────────────────────────────────

    [Fact]
    public void Map_WhenCreated_ParsedFromGeneralizedTime()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()
        {
            ["whenCreated"] = "20230115083045.0Z",
            ["userAccountControl"] = "512",
        }));

        Assert.NotNull(dto.CreatedInAd);
        Assert.Equal(2023, dto.CreatedInAd!.Value.Year);
        Assert.Equal(1,    dto.CreatedInAd.Value.Month);
        Assert.Equal(15,   dto.CreatedInAd.Value.Day);
        Assert.Equal(DateTimeKind.Utc, dto.CreatedInAd.Value.Kind);
    }

    [Fact]
    public void Map_WhenChanged_ParsedFromGeneralizedTime()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()
        {
            ["whenChanged"] = "20240601120000.0Z",
            ["userAccountControl"] = "512",
        }));

        Assert.NotNull(dto.ModifiedInAd);
        Assert.Equal(2024, dto.ModifiedInAd!.Value.Year);
        Assert.Equal(6,    dto.ModifiedInAd.Value.Month);
        Assert.Equal(1,    dto.ModifiedInAd.Value.Day);
    }

    [Fact]
    public void Map_MissingDates_NullDates()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));
        Assert.Null(dto.CreatedInAd);
        Assert.Null(dto.ModifiedInAd);
    }

    [Fact]
    public void Map_InvalidDateString_NullDate()
    {
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new() { ["whenCreated"] = "not-a-date" }));
        Assert.Null(dto.CreatedInAd);
    }

    // ── LastSyncedAt ──────────────────────────────────────────────────────────

    [Fact]
    public void Map_LastSyncedAt_IsApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var dto = LdapToUserDtoMapper.Map(BuildEntry(new()));
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(dto.LastSyncedAt, before, after);
    }
}
