using CompanyDirectory.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyDirectory.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class VersionController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new VersionInfoDto
    {
        Version     = configuration["Updates:CurrentVersion"] ?? "1.0.0",
        DownloadUrl = configuration["Updates:DownloadUrl"]    ?? string.Empty,
    });
}
