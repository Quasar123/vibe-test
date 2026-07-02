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
[Route("api/tests")]
[EnableRateLimiting(RateLimitPolicies.GlobalApi)]
public class TestsController(
    ITestService testService,
    IResultService resultService,
    IUserService userService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResponse<TestListItem>> GetPublicTests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] string order = "desc") =>
        testService.GetPublicTests(page, pageSize, sortBy, order);

    [HttpGet("my")]
    [Authorize]
    public Task<PagedResponse<TestListItem>> GetMyTests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string filter = "all",
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] string order = "desc") =>
        testService.GetMyTests(User.GetUserId(), page, pageSize, filter, sortBy, order);

    [HttpGet("my/stats")]
    [Authorize]
    public Task<UserStatsResponse> GetMyStats() =>
        userService.GetStats(User.GetUserId());

    [HttpGet("progress")]
    [Authorize]
    public Task<TestProgressListResponse> GetTestsProgress([FromQuery] int[] ids) =>
        resultService.GetUserTestProgress(User.GetUserId(), ids);

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public Task<TestDetailResponse> GetTestDetail(int id) =>
        testService.GetTestDetail(id, User.Identity?.IsAuthenticated == true ? User.GetUserId() : null);

    [HttpGet("{id:int}/full")]
    [Authorize]
    public Task<TestFullResponse> GetTestFull(int id) =>
        testService.GetTestFull(id, User.GetUserId());

    [HttpGet("{id:int}/play-public")]
    [AllowAnonymous]
    public Task<TestFullResponse> GetPublicPlayTest(int id) =>
        testService.GetPublicPlayTest(id);

    [HttpPost]
    [Authorize]
    public Task<TestResponse> CreateTest([FromBody] CreateTestRequest request) =>
        testService.CreateTest(User.GetUserId(), request);

    [HttpPatch("{id:int}")]
    [Authorize]
    public Task<TestResponse> AppendQuestions(int id, [FromBody] AddQuestionsRequest request) =>
        testService.AppendQuestions(id, User.GetUserId(), request);

    [HttpPatch("{id:int}/info")]
    [Authorize]
    public Task<TestResponse> UpdateTestInfo(int id, [FromBody] UpdateTestInfoRequest request) =>
        testService.UpdateTestInfo(id, User.GetUserId(), request);

    [HttpPut("{id:int}/publish")]
    [Authorize]
    public Task PublishTest(int id)
    {
        return testService.PublishTest(id, User.GetUserId());
    }

    [HttpPut("{id:int}/unpublish")]
    [Authorize]
    public Task UnpublishTest(int id) =>
        testService.UnpublishTest(id, User.GetUserId());

    [HttpDelete("{id:int}")]
    [Authorize]
    public Task DeleteTest(int id) =>
        testService.DeleteTest(id, User.GetUserId());

    [HttpPost("{id:int}/submit")]
    [Authorize]
    public Task<SubmitResponse> SubmitAnswer(int id, [FromBody] SubmitAnswerRequest request) =>
        resultService.SubmitAnswer(User.GetUserId(), id, request);

    [HttpGet("{id:int}/answers")]
    [Authorize]
    public Task<AnsweredQuestionsResponse> GetAnsweredQuestions(int id) =>
        resultService.GetAnsweredQuestions(User.GetUserId(), id);

    [HttpGet("{id:int}/result")]
    [Authorize]
    public Task<TestResultResponse> GetResult(int id) =>
        resultService.GetResult(User.GetUserId(), id);

    [HttpDelete("{id:int}/result")]
    [Authorize]
    public Task DeleteResult(int id) =>
        resultService.DeleteResult(User.GetUserId(), id);
}
