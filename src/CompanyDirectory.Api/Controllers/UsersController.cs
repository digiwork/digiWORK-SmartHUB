using CompanyDirectory.Api.Services;
using CompanyDirectory.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CompanyDirectory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class UsersController(
    IUserDirectoryService directoryService,
    IOptions<SearchSettings> searchOptions,
    ILogger<UsersController> logger) : ControllerBase
{
    private static readonly Regex LoginRegex = LoginRegexSource();
    private readonly SearchSettings _search = searchOptions.Value;

    // GET /api/users/all  — returns all active users for Desktop cache sync
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        logger.LogDebug("GetAll called by {User}", User.Identity?.Name);
        var users = await directoryService.GetAllUsersAsync(ct);
        return Ok(users);
    }

    // GET /api/users/search?q=kowalski
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        CancellationToken ct)
    {
        q = q?.Trim() ?? string.Empty;

        if (q.Length < _search.MinimumQueryLength)
            return BadRequest(new { error = $"Query must be at least {_search.MinimumQueryLength} characters." });

        if (q.Length > 100)
            return BadRequest(new { error = "Query must not exceed 100 characters." });

        logger.LogDebug("User search: query length {Len}", q.Length);

        var result = await directoryService.SearchAsync(q, ct);
        return Ok(result);
    }

    // GET /api/users/{login}
    [HttpGet("{login}")]
    public async Task<IActionResult> GetByLogin(string login, CancellationToken ct)
    {
        if (!IsValidLogin(login))
            return BadRequest(new { error = "Invalid login format." });

        var user = await directoryService.GetByLoginAsync(login, ct);
        return user is null ? NotFound() : Ok(user);
    }

    // GET /api/users/{login}/manager
    [HttpGet("{login}/manager")]
    public async Task<IActionResult> GetManager(string login, CancellationToken ct)
    {
        if (!IsValidLogin(login))
            return BadRequest(new { error = "Invalid login format." });

        var manager = await directoryService.GetManagerAsync(login, ct);
        return manager is null ? NotFound() : Ok(manager);
    }

    // GET /api/users/{login}/groups
    [HttpGet("{login}/groups")]
    public async Task<IActionResult> GetGroups(string login, CancellationToken ct)
    {
        if (!IsValidLogin(login))
            return BadRequest(new { error = "Invalid login format." });

        var groups = await directoryService.GetGroupsAsync(login, ct);
        return Ok(groups);
    }

    // POST /api/users/sync  — admin only
    [HttpPost("sync")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        logger.LogInformation("Manual sync triggered by {User}", User.Identity?.Name);
        var result = await directoryService.SyncAllAsync(ct);
        return Ok(result);
    }

    private static bool IsValidLogin(string login)
        => !string.IsNullOrEmpty(login) && LoginRegex.IsMatch(login);

    [GeneratedRegex(@"^[a-zA-Z0-9._\-]{1,64}$", RegexOptions.Compiled)]
    private static partial Regex LoginRegexSource();
}
