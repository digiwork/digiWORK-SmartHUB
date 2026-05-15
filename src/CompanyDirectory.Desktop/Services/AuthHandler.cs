using System.Net.Http.Headers;

namespace CompanyDirectory_Desktop.Services;

public class AuthHandler : DelegatingHandler
{
    private readonly AuthService _auth;

    public AuthHandler(AuthService auth) : base(new HttpClientHandler { AllowAutoRedirect = true })
        => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
