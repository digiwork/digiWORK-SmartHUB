using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace CompanyDirectory_Desktop.Services;

public class AuthService
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly ILogger<AuthService> _logger;

    // Set by MainWindow after it's created — needed for WAM parent window
    private nint _parentHwnd;
    public void SetParentWindow(nint hwnd) => _parentHwnd = hwnd;

    public AuthService(IOptions<AzureAdSettings> options, ILogger<AuthService> logger)
    {
        _logger = logger;
        var s = options.Value;
        _scopes = [$"api://{s.ApiClientId}/access_as_user"];

        // WAM (Windows Account Manager) — integrates with Windows Hello / Entra ID SSO,
        // no browser window needed; falls back to embedded WebView2 if WAM is unavailable
        _app = PublicClientApplicationBuilder
            .Create(s.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, s.TenantId)
            .WithDefaultRedirectUri()
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var accounts = await _app.GetAccountsAsync();
        var account  = accounts.FirstOrDefault();

        // 1. Silent — cached token or WAM SSO (no user interaction)
        var silentAccount = account ?? PublicClientApplication.OperatingSystemAccount;
        try
        {
            var result = await _app.AcquireTokenSilent(_scopes, silentAccount).ExecuteAsync(ct);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException) { }

        // 2. Interactive — WAM dialog (Windows Hello / Microsoft account picker)
        //    No browser window opens; WAM shows a native Windows dialog
        _logger.LogInformation("Auth: showing WAM login dialog");
        var interactive = await _app
            .AcquireTokenInteractive(_scopes)
            .WithPrompt(Prompt.SelectAccount)
            .WithParentActivityOrWindow(_parentHwnd)
            .ExecuteAsync(ct);
        return interactive.AccessToken;
    }
}
