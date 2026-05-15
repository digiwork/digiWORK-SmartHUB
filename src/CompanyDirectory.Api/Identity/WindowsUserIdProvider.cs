using Microsoft.AspNetCore.SignalR;

namespace CompanyDirectory.Api.Identity;

// Handles both Windows Auth (DOMAIN\login) and JWT Bearer (login@domain.com)
public class WindowsUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        if (user is null) return null;

        var name = user.Identity?.Name ?? string.Empty;

        // Windows Auth: DOMAIN\login → login
        if (name.Contains('\\'))
            return name.Split('\\')[1].ToLowerInvariant();

        // JWT: preferred_username = login@domain.com → login
        var upn = user.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? string.Empty;
        if (upn.Contains('@'))
            return upn.Split('@')[0].ToLowerInvariant();

        // JWT fallback: name claim may contain UPN
        if (name.Contains('@'))
            return name.Split('@')[0].ToLowerInvariant();

        return name.ToLowerInvariant();
    }
}
