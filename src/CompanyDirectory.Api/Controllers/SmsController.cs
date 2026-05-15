using CompanyDirectory.Api.Sms;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CompanyDirectory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsController(ISmsGateway gateway, IOptions<SmsSettings> options, ILogger<SmsController> logger) : ControllerBase
{
    private readonly SmsSettings _cfg = options.Value;

    private string GetLogin() =>
        (User.Identity?.Name ?? string.Empty)
        .Split('\\').Last().ToLowerInvariant();

    private bool IsAdmin(string login)
    {
        var admins = _cfg.AdminLogins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.ToLowerInvariant());
        return admins.Contains(login);
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SmsSendDto dto)
    {
        var login = GetLogin();
        if (!IsAdmin(login)) return Forbid();

        if (dto.PhoneNumbers.Count == 0 || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest("Brak numerów lub treści wiadomości");

        logger.LogInformation("SMS send request by {Login}: {Count} numbers", login, dto.PhoneNumbers.Count);

        var result = await gateway.SendAsync(dto.PhoneNumbers, dto.Message, _cfg.SenderName);

        return Ok(new SmsSendResultDto
        {
            Sent         = result.Sent,
            Failed       = result.Failed,
            ErrorMessage = result.Error,
        });
    }
}
