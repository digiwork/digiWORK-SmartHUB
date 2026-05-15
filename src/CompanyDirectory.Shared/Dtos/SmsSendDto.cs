namespace CompanyDirectory.Shared.Dtos;

public class SmsSendDto
{
    public List<string> PhoneNumbers { get; set; } = [];
    public string Message            { get; set; } = string.Empty;
}
