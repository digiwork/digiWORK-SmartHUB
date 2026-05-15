namespace CompanyDirectory.Api.Sms;

public interface ISmsGateway
{
    Task<SmsGatewayResult> SendAsync(IEnumerable<string> phoneNumbers, string message, string sender, CancellationToken ct = default);
}

public record SmsGatewayResult(int Sent, int Failed, string? Error = null);
