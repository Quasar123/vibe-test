using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VibeTest.Server.Configuration;
using VibeTest.Server.Extensions;
using VibeTest.Server.Models.Responses;
using VibeTest.Server.Services;

namespace VibeTest.Server.Controllers;

[ApiController]
[Route("api/results")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.GlobalApi)]
public class ResultsController(IResultService resultService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<TestHistoryItem>> GetUserResults(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "date",
        [FromQuery] string order = "desc") =>
        resultService.GetUserResults(User.GetUserId(), page, pageSize, sortBy, order);
}
