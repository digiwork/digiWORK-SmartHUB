using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace CompanyDirectory_Desktop.Services;

public sealed class ApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IOptions<ApiClientSettings> options, AuthService auth, ILogger<ApiClient> logger)
    {
        _logger = logger;

        var baseUrl = options.Value.BaseUrl.TrimEnd('/');

        _http = new HttpClient(new AuthHandler(auth))
        {
            BaseAddress = new Uri(baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<List<UserDirectoryEntryDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        try
        {
            var users = await _http.GetFromJsonAsync<List<UserDirectoryEntryDto>>(
                "api/users/all", ct);

            _logger.LogInformation("GetAllUsersAsync returned {Count} users", users?.Count ?? 0);
            return users ?? [];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("API request timed out (GET /api/users/all)");
            return [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "API unreachable (GET /api/users/all) — offline mode");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetAllUsersAsync");
            return [];
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("api/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<VersionInfoDto?> GetVersionInfoAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<VersionInfoDto>("api/version", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Version check failed");
            return null;
        }
    }

    public async Task<List<MessageDto>> GetReadMessagesAsync(CancellationToken ct = default)
    {
        try
        {
            var msgs = await _http.GetFromJsonAsync<List<MessageDto>>("api/messages/read", ct);
            return msgs ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetReadMessagesAsync failed");
            return [];
        }
    }

    public async Task<List<MessageDto>> GetSentMessagesAsync(CancellationToken ct = default)
    {
        try
        {
            var msgs = await _http.GetFromJsonAsync<List<MessageDto>>("api/messages/sent", ct);
            return msgs ?? [];
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("GetSentMessagesAsync — access denied (not an admin)");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetSentMessagesAsync failed");
            return [];
        }
    }

    public async Task<SmsSendResultDto> SendSmsAsync(SmsSendDto dto, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/sms/send", dto, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return new SmsSendResultDto { ErrorMessage = "Brak uprawnień (wymagane konto administratora)" };
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SmsSendResultDto>(ct)
                   ?? new SmsSendResultDto { ErrorMessage = "Pusta odpowiedź serwera" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendSmsAsync failed");
            return new SmsSendResultDto { ErrorMessage = ex.Message };
        }
    }

    public void Dispose() => _http.Dispose();
}
