namespace CompanyDirectory.Shared.Dtos;

public class ConversationSummaryDto
{
    public string   OtherLogin          { get; set; } = string.Empty;
    public string   OtherDisplayName    { get; set; } = string.Empty;
    public string   LastMessagePreview  { get; set; } = string.Empty;
    public DateTime LastMessageAt       { get; set; }
    public int      UnreadCount         { get; set; }
    public bool     LastMessageIsOwn    { get; set; }
}
