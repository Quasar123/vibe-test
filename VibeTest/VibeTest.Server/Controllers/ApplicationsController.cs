using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VibeTest.Server.Configuration;
using VibeTest.Server.Extensions;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Server.Services;

namespace VibeTest.Server.Controllers;

[ApiController]
[Route("api/applications")]
[EnableRateLimiting(RateLimitPolicies.GlobalApi)]
public class ApplicationsController(IApplicationService applicationService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public Task<ApplicationResponse> Create([FromBody] CreateApplicationRequest request) =>
        applicationService.CreateApplication(User.GetUserId(), request);

    [HttpGet]
    [Authorize]
    public Task<PagedResponse<ApplicationListItem>> GetMy(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        applicationService.GetMyApplications(User.GetUserId(), page, pageSize);

    [HttpGet("incoming")]
    [Authorize]
    public Task<PagedResponse<IncomingApplicationListItem>> GetIncoming(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        applicationService.GetIncomingApplications(User.GetUserId(), page, pageSize);

    [HttpGet("{token:guid}")]
    [AllowAnonymous]
    public Task<ApplicationPlayResponse> GetDetail(Guid token) =>
        applicationService.GetApplicationPlayDetail(token, GetCurrentUserId());

    [HttpPost("{token:guid}/submit")]
    [AllowAnonymous]
    public Task<SubmitResponse> SubmitAnswer(Guid token, [FromBody] SubmitAnswerRequest request) =>
        applicationService.SubmitAnswer(token, request, GetCurrentUserId());

    [HttpGet("{token:guid}/answers")]
    [AllowAnonymous]
    public Task<AnsweredQuestionsResponse> GetAnsweredQuestions(Guid token) =>
        applicationService.GetApplicationAnsweredQuestions(token, GetCurrentUserId());

    [HttpGet("{token:guid}/result")]
    [AllowAnonymous]
    public Task<TestResultResponse> GetResult(Guid token) =>
        applicationService.GetApplicationResult(token, GetCurrentUserId());

    private int? GetCurrentUserId() =>
        User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
}
