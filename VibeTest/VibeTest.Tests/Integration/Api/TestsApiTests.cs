using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class TestsApiTests(PostgreSqlTestFixture postgres)
{
    private readonly ApiWebApplicationFactory _factory = postgres.Factory;

    [Fact]
    public async Task Create_publish_and_get_public_test()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client);
        _factory.Authorize(client, token);

        var create = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var publish = await client.PutAsync($"/api/tests/{created!.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var list = await client.GetAsync("/api/tests?page=1&pageSize=10");
        var page = await list.Content.ReadFromJsonAsync<PagedResponse<TestListItem>>(_factory.JsonOptions);
        Assert.Contains(page!.Items, t => t.Id == created.Id);

        var detail = await client.GetAsync($"/api/tests/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<TestDetailResponse>(_factory.JsonOptions);
        Assert.Equal(2, detailBody!.Questions.Count);
        Assert.DoesNotContain(detailBody.Questions.SelectMany(q => q.Answers), a => a.GetType().GetProperty("IsCorrect") != null);
    }

    [Fact]
    public async Task Public_list_supports_name_sort_and_returns_updatedAt()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client);
        _factory.Authorize(client, token);

        var createZ = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest
        {
            Name = "Zebra Test",
            Questions = ServiceFixture.SampleTestRequest().Questions
        });
        Assert.Equal(HttpStatusCode.OK, createZ.StatusCode);
        var zebra = await createZ.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var createA = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest
        {
            Name = "Alpha Test",
            Questions = ServiceFixture.SampleTestRequest().Questions
        });
        Assert.Equal(HttpStatusCode.OK, createA.StatusCode);
        var alpha = await createA.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        await client.PutAsync($"/api/tests/{zebra!.Id}/publish", null);
        await client.PutAsync($"/api/tests/{alpha!.Id}/publish", null);

        client.DefaultRequestHeaders.Authorization = null;
        var list = await client.GetAsync("/api/tests?page=1&pageSize=10&sortBy=name&order=asc");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResponse<TestListItem>>(_factory.JsonOptions);

        Assert.NotNull(page);
        Assert.Contains(page.Items, t => t.Id == zebra.Id && t.UpdatedAt != default);
        Assert.Contains(page.Items, t => t.Id == alpha.Id && t.UpdatedAt != default);

        var names = page.Items.Select(t => t.Name).ToList();
        var alphaIndex = names.IndexOf("Alpha Test");
        var zebraIndex = names.IndexOf("Zebra Test");
        Assert.True(alphaIndex >= 0);
        Assert.True(zebraIndex >= 0);
        Assert.True(alphaIndex < zebraIndex);
    }

    [Fact]
    public async Task Submit_and_get_result()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client);
        _factory.Authorize(client, token);

        var create = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);
        await client.PutAsync($"/api/tests/{created!.Id}/publish", null);

        var submit = await client.PostAsJsonAsync($"/api/tests/{created.Id}/submit", new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitBody = await submit.Content.ReadFromJsonAsync<SubmitResponse>(_factory.JsonOptions);
        Assert.Equal(0, submitBody!.CorrectAnswerOrder);

        var answers = await client.GetAsync($"/api/tests/{created.Id}/answers");
        Assert.Equal(HttpStatusCode.OK, answers.StatusCode);
        var answersBody = await answers.Content.ReadFromJsonAsync<AnsweredQuestionsResponse>(_factory.JsonOptions);
        Assert.Single(answersBody!.Answers);
        Assert.Equal(0, answersBody.Answers[0].QuestionOrder);

        var repeat = await client.PostAsJsonAsync($"/api/tests/{created.Id}/submit", new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);

        var result = await client.GetAsync($"/api/tests/{created.Id}/result");
        var resultBody = await result.Content.ReadFromJsonAsync<TestResultResponse>(_factory.JsonOptions);
        Assert.Equal(1, resultBody!.CorrectAnswers);
    }

    [Fact]
    public async Task Unauthorized_create_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Append_forbidden_for_other_user_returns_403()
    {
        var client = _factory.CreateClient();
        var authorToken = await _factory.RegisterAndGetTokenAsync(client, $"author-{Guid.NewGuid():N}@test.com");
        _factory.Authorize(client, authorToken);

        var create = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        var created = await create.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var otherToken = await _factory.RegisterAndGetTokenAsync(client, $"other-{Guid.NewGuid():N}@test.com");
        _factory.Authorize(client, otherToken);

        var append = await ApiWebApplicationFactory.PatchAsJsonAsync(client, $"/api/tests/{created!.Id}", new AddQuestionsRequest
        {
            Questions = ServiceFixture.SampleTestRequest().Questions
        });

        Assert.Equal(HttpStatusCode.Forbidden, append.StatusCode);
    }
}
