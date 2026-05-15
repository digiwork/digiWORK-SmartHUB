namespace CompanyDirectory.Shared.Dtos;

public class MessageDto
{
    public int    Id                   { get; set; }
    public string Title                { get; set; } = string.Empty;
    public string Body                 { get; set; } = string.Empty;
    public string AuthorLogin          { get; set; } = string.Empty;
    public string AuthorDisplayName    { get; set; } = string.Empty;
    public DateTime CreatedAt          { get; set; }
    public bool   RequiresConfirmation { get; set; } = true;
}
