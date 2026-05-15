using CompanyDirectory.Shared.Dtos;

namespace CompanyDirectory.Api.Data;

public interface IChatRepository
{
    Task<ChatMessageDto> SendAsync(ChatMessageDto message);
    Task<List<ChatMessageDto>> GetHistoryAsync(string loginA, string loginB, int limit = 50);
    Task<List<ConversationSummaryDto>> GetConversationsAsync(string myLogin);
    Task MarkAsReadAsync(string fromLogin, string toLogin);
}
