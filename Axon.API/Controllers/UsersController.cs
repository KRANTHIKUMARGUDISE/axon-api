using System.Security.Claims;
using Axon.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;

    public UsersController(IUserRepository users)
    {
        _users = users;
    }

    [HttpGet("teams")]
    public async Task<IActionResult> GetTeams()
    {
        var allUsers = await _users.GetAllAsync();
        var teams = allUsers
            .Select(u => u.Team)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        return Ok(teams);
    }
}
