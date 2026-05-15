using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CompanyDirectory.Api.Sms;

public sealed class SmsApiGateway(HttpClient http, IOptions<SmsSettings> options, ILogger<SmsApiGateway> logger) : ISmsGateway
{
    private readonly SmsSettings _cfg = options.Value;

    public async Task<SmsGatewayResult> SendAsync(
        IEnumerable<string> phoneNumbers,
        string message,
        string sender,
        CancellationToken ct = default)
    {
        var numbers = phoneNumbers
            .Select(NormalizePhone)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        if (numbers.Count == 0)
            return new SmsGatewayResult(0, 0, "Brak prawidłowych numerów");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["to"]      = string.Join(",", numbers),
            ["message"] = message,
            ["from"]    = string.IsNullOrWhiteSpace(sender) ? _cfg.SenderName : sender,
            ["format"]  = "json",
        });

        try
        {
            var response = await http.PostAsync("sms.do", form, ct);
            var json     = await response.Content.ReadAsStringAsync(ct);
            logger.LogDebug("SMSAPI response: {StatusCode} {Body}", response.StatusCode, json);

            var root = JsonSerializer.Deserialize<SmsApiResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!response.IsSuccessStatusCode || root?.Error is not null)
            {
                var detail = root?.Message ?? $"HTTP {(int)response.StatusCode}";
                logger.LogWarning("SMSAPI error {Code}: {Detail}", root?.Error, detail);
                return new SmsGatewayResult(0, numbers.Count, $"SMSAPI: {detail}");
            }

            if (root?.List is { } list)
            {
                var sent   = list.Count(i => i.Status is "QUEUE" or "SENT" or "DELIVERED");
                var failed = list.Count - sent;
                return new SmsGatewayResult(sent, failed);
            }

            return new SmsGatewayResult(0, numbers.Count, "Nieoczekiwana odpowiedź bramy SMS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMSAPI request failed");
            return new SmsGatewayResult(0, numbers.Count, ex.Message);
        }
    }

    private static string NormalizePhone(string raw)
    {
        var digits = Regex.Replace(raw, @"\D", "");
        return digits.Length switch
        {
            9  => "48" + digits,
            11 when digits.StartsWith("48") => digits,
            12 when digits.StartsWith("048") => digits[1..],
            _  => digits.Length >= 9 ? digits : string.Empty,
        };
    }

    private class SmsApiResponse
    {
        [JsonPropertyName("count")]   public int Count { get; set; }
        [JsonPropertyName("list")]    public List<SmsApiItem>? List { get; set; }
        [JsonPropertyName("error")]   public int? Error { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    private class SmsApiItem
    {
        [JsonPropertyName("id")]     public string Id     { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("number")] public string Number { get; set; } = "";
    }
}
