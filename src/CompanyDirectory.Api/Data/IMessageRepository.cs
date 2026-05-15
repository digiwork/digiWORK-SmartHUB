using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory.Api.Data;

public interface IMessageRepository
{
    Task<int> CreateAsync(MessageDto message);
    Task<List<MessageDto>> GetUnreadAsync(string userLogin);
    Task<List<MessageDto>> GetReadAsync(string userLogin);
    Task<List<MessageDto>> GetAllAsync();
    Task MarkAsReadAsync(int messageId, string userLogin);
}
