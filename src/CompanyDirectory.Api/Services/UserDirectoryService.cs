using CompanyDirectory.Api.Mapping;
using CompanyDirectory.Infrastructure.Ldap;
using CompanyDirectory.Shared.Dtos;
using Microsoft.Extensions.Options;
using CompanyDirectory.Shared.Configuration;

namespace CompanyDirectory.Api.Services;

public class UserDirectoryService(
    ILdapService ldap,
    IOptions<SearchSettings> searchOptions,
    ILogger<UserDirectoryService> logger) : IUserDirectoryService
{
    private readonly SearchSettings _search = searchOptions.Value;

    public async Task<UserSearchResultDto> SearchAsync(string query, CancellationToken ct = default)
    {
        var raw = await ldap.SearchUsersAsync(query, ct);
        var items = raw
            .Select(LdapToUserDtoMapper.Map)
            .Take(_search.MaxResults)
            .ToList();

        return new UserSearchResultDto
        {
            Items      = items,
            TotalCount = items.Count,
            Query      = query,
            Timestamp  = DateTime.UtcNow,
        };
    }

    public async Task<UserDirectoryEntryDto?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        var raw = await ldap.GetUserByLoginAsync(login, ct);
        if (raw is null) return null;

        var dto = LdapToUserDtoMapper.Map(raw);
        await ResolveManagerDisplayNameAsync(dto, ct);
        return dto;
    }

    public async Task<UserDirectoryEntryDto?> GetManagerAsync(string login, CancellationToken ct = default)
    {
        var user = await ldap.GetUserByLoginAsync(login, ct);
        if (user is null) return null;

        var managerDn = user.GetString("manager");
        if (string.IsNullOrEmpty(managerDn)) return null;

        // Search manager by DN
        var managers = await ldap.SearchUsersAsync(string.Empty, ct);
        var managerRaw = managers.FirstOrDefault(u =>
            string.Equals(u.DistinguishedName, managerDn, StringComparison.OrdinalIgnoreCase));

        return managerRaw is null ? null : LdapToUserDtoMapper.Map(managerRaw);
    }

    public async Task<List<string>> GetGroupsAsync(string login, CancellationToken ct = default)
    {
        var user = await ldap.GetUserByLoginAsync(login, ct);
        if (user is null) return [];

        return await ldap.GetUserGroupsAsync(user.DistinguishedName, ct);
    }

    public async Task<List<UserDirectoryEntryDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var raw = await ldap.GetAllActiveUsersAsync(ct);
        var dtos = raw.Select(LdapToUserDtoMapper.Map).ToList();

        // Resolve manager display names using in-memory lookup — no extra LDAP queries
        var dnToDisplayName = raw.ToDictionary(
            r => r.DistinguishedName,
            r => r.GetString("displayName") ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dtos)
        {
            if (!string.IsNullOrEmpty(dto.ManagerDistinguishedName) &&
                dnToDisplayName.TryGetValue(dto.ManagerDistinguishedName, out var managerName))
            {
                dto.ManagerDisplayName = managerName;
            }
        }

        return dtos;
    }

    public async Task<SyncResultDto> SyncAllAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var errors = new List<string>();
        int count = 0;

        try
        {
            logger.LogInformation("Full AD sync started");
            var raw = await ldap.GetAllActiveUsersAsync(ct);
            count = raw.Count;
            logger.LogInformation("Full AD sync completed: {Count} users in {ElapsedMs}ms",
                count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Full AD sync failed");
            errors.Add(ex.Message);
        }

        sw.Stop();
        return new SyncResultDto
        {
            SyncedCount = count,
            Duration    = sw.Elapsed,
            Errors      = errors,
        };
    }

    private async Task ResolveManagerDisplayNameAsync(UserDirectoryEntryDto dto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(dto.ManagerDistinguishedName)) return;

        try
        {
            // Extract CN from DN to do a targeted search
            var cn = ExtractCn(dto.ManagerDistinguishedName);
            if (string.IsNullOrEmpty(cn)) return;

            var candidates = await ldap.SearchUsersAsync(cn, ct);
            var manager = candidates.FirstOrDefault(u =>
                string.Equals(u.DistinguishedName, dto.ManagerDistinguishedName, StringComparison.OrdinalIgnoreCase));

            if (manager is not null)
                dto.ManagerDisplayName = manager.GetString("displayName") ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve manager display name for {DN}", dto.ManagerDistinguishedName);
        }
    }

    private static string ExtractCn(string distinguishedName)
    {
        // CN=Jan Kowalski,OU=... → "Jan Kowalski"
        var part = distinguishedName.Split(',')[0];
        var eq = part.IndexOf('=');
        return eq >= 0 ? part[(eq + 1)..] : string.Empty;
    }
}
