using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory_Desktop.LocalCache;

public interface IUserCacheRepository
{
    Task<int> UpsertUsersAsync(IEnumerable<UserDirectoryEntryDto> users);
    Task<List<UserDirectoryEntryDto>> SearchAsync(string query, int maxResults);
    Task<List<UserDirectoryEntryDto>> GetAllActiveAsync(string? companyFilter = null);
    Task<UserDirectoryEntryDto?> GetByLoginAsync(string login);
    Task<UserDirectoryEntryDto?> GetByDistinguishedNameAsync(string dn);
    Task<int> GetUserCountAsync();
    Task ClearAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
}
