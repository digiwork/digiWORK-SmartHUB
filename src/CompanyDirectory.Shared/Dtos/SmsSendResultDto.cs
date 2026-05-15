namespace CompanyDirectory.Shared.Dtos;

public class SmsSendResultDto
{
    public int    Sent         { get; set; }
    public int    Failed       { get; set; }
    public string? ErrorMessage { get; set; }
}
