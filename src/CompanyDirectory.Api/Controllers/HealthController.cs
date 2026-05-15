using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace CompanyDirectory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "unknown";

        return Ok(new
        {
            status = "healthy",
            version,
            timestamp = DateTime.UtcNow
        });
    }
}
