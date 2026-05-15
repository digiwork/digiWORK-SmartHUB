using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory.Api.Services;

public interface IUserDirectoryService
{
    Task<UserSearchResultDto> SearchAsync(string query, CancellationToken ct = default);
    Task<UserDirectoryEntryDto?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<UserDirectoryEntryDto?> GetManagerAsync(string login, CancellationToken ct = default);
    Task<List<string>> GetGroupsAsync(string login, CancellationToken ct = default);
    Task<List<UserDirectoryEntryDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<SyncResultDto> SyncAllAsync(CancellationToken ct = default);
}
