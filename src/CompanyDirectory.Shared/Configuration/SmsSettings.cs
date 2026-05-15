namespace CompanyDirectory.Shared.Configuration;

public class SmsSettings
{
    public const string SectionName = "Sms";

    public string ApiToken    { get; set; } = string.Empty;
    public string SenderName  { get; set; } = "FIRMA";
    public string ApiBaseUrl  { get; set; } = "https://api.smsapi.com";
    public string AdminLogins { get; set; } = string.Empty;
}
