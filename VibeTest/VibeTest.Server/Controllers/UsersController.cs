using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VibeTest.Server.Configuration;
using VibeTest.Server.Data.Repositories;
using VibeTest.Server.Exceptions;
using VibeTest.Server.Extensions;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Controllers;

[ApiController]
[Route("api/users")]
[EnableRateLimiting(RateLimitPolicies.GlobalApi)]
public class UsersController(IUserRepository users) : ControllerBase
{
    [HttpGet("search")]
    [Authorize]
    public async Task<IReadOnlyList<UserSearchResult>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            throw new ValidationException("Укажите не менее 2 символов для поиска");

        var normalizedLimit = Math.Clamp(limit, 1, 20);
        return await users.SearchAsync(q.Trim(), User.GetUserId(), normalizedLimit);
    }
}
