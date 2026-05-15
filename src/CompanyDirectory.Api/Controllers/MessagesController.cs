using CompanyDirectory.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace CompanyDirectory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController(IMessageRepository repository, IConfiguration configuration) : ControllerBase
{
    private string GetLogin() =>
        (User.Identity?.Name ?? string.Empty)
        .Split('\\').Last().ToLowerInvariant();

    private bool IsAdmin(string login)
    {
        var admins = (configuration["Messages:AdminLogins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.ToLowerInvariant());
        return admins.Contains(login);
    }

    [HttpGet("read")]
    public async Task<IActionResult> GetRead()
    {
        var login    = GetLogin();
        var messages = await repository.GetReadAsync(login);
        return Ok(messages);
    }

    [HttpGet("sent")]
    public async Task<IActionResult> GetSent()
    {
        var login = GetLogin();
        if (!IsAdmin(login)) return Forbid();
        var messages = await repository.GetAllAsync();
        return Ok(messages);
    }
}
