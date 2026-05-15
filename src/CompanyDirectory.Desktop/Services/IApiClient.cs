using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory_Desktop.Services;

public interface IApiClient
{
    Task<List<UserDirectoryEntryDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
    Task<VersionInfoDto?> GetVersionInfoAsync(CancellationToken ct = default);
    Task<List<MessageDto>> GetReadMessagesAsync(CancellationToken ct = default);
    Task<List<MessageDto>> GetSentMessagesAsync(CancellationToken ct = default);
    Task<SmsSendResultDto> SendSmsAsync(SmsSendDto dto, CancellationToken ct = default);
}
