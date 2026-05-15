namespace CompanyDirectory.Shared.Dtos;

public class ChatMessageDto
{
    public int      Id              { get; set; }
    public string   FromLogin       { get; set; } = string.Empty;
    public string   FromDisplayName { get; set; } = string.Empty;
    public string   ToLogin         { get; set; } = string.Empty;
    public string   ToDisplayName   { get; set; } = string.Empty;
    public string   Body            { get; set; } = string.Empty;
    public DateTime SentAt          { get; set; }
    public bool     IsRead          { get; set; }
}
