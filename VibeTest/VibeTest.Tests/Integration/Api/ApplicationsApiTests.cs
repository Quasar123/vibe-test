using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class ApplicationsApiTests(PostgreSqlTestFixture postgres)
{
    private readonly ApiWebApplicationFactory _factory = postgres.Factory;

    [Fact]
    public async Task Create_list_and_anonymous_play_flow()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client);
        _factory.Authorize(client, token);

        var test = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        var createdTest = await test.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var createApp = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Eve",
            TestId = createdTest!.Id
        });
        Assert.Equal(HttpStatusCode.OK, createApp.StatusCode);
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);
        Assert.NotNull(application);
        Assert.Equal("Eve", application.Title);
        Assert.Equal(ApplicationType.Link, application.Type);

        var list = await client.GetAsync("/api/applications?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResponse<ApplicationListItem>>(_factory.JsonOptions);
        Assert.Contains(page!.Items, a => a.Id == application.Id);

        client.DefaultRequestHeaders.Authorization = null;

        var detail = await client.GetAsync($"/api/applications/{application.Token}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<ApplicationPlayResponse>(_factory.JsonOptions);
        Assert.Equal(2, detailBody!.Questions.Count);
        Assert.False(detailBody.HideResultsFromParticipant);
        Assert.False(detailBody.IsCompleted);

        var submit = await client.PostAsJsonAsync(
            $"/api/applications/{application.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var result = await client.GetAsync($"/api/applications/{application.Token}/result");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var resultBody = await result.Content.ReadFromJsonAsync<TestResultResponse>(_factory.JsonOptions);
        Assert.Equal(1, resultBody!.CorrectAnswers);
    }

    [Fact]
    public async Task Create_with_hideResults_returns_flag_in_play_response()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client);
        _factory.Authorize(client, token);

        var test = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        var createdTest = await test.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var createApp = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Hidden",
            TestId = createdTest!.Id,
            HideResultsFromParticipant = true
        });
        Assert.Equal(HttpStatusCode.OK, createApp.StatusCode);
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);
        Assert.True(application!.HideResultsFromParticipant);

        client.DefaultRequestHeaders.Authorization = null;

        var detail = await client.GetAsync($"/api/applications/{application.Token}");
        var detailBody = await detail.Content.ReadFromJsonAsync<ApplicationPlayResponse>(_factory.JsonOptions);
        Assert.True(detailBody!.HideResultsFromParticipant);
        Assert.False(detailBody.IsCompleted);
    }

    [Fact]
    public async Task Create_application_requires_auth()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "No Auth",
            TestId = 1
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_create_internal_and_get_incoming()
    {
        var authorClient = _factory.CreateClient();
        var authorToken = await RegisterUserAsync(authorClient, "author@test.com", "Author");
        _factory.Authorize(authorClient, authorToken);

        var recipientClient = _factory.CreateClient();
        var recipientToken = await RegisterUserAsync(recipientClient, "recipient@test.com", "Recipient");

        var search = await authorClient.GetAsync("/api/users/search?q=reci");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var users = await search.Content.ReadFromJsonAsync<List<UserSearchResult>>(_factory.JsonOptions);
        Assert.Contains(users!, u => u.DisplayName == "Recipient");

        var test = await authorClient.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        var createdTest = await test.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var recipientId = users!.First(u => u.DisplayName == "Recipient").Id;
        var createApp = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Quarterly review",
            Type = ApplicationType.InternalUser,
            TestId = createdTest!.Id,
            RecipientUserId = recipientId
        });
        Assert.Equal(HttpStatusCode.OK, createApp.StatusCode);
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);
        Assert.Equal("Quarterly review", application!.Title);
        Assert.Equal(ApplicationType.InternalUser, application.Type);
        Assert.Equal(string.Empty, application.PlayUrl);

        _factory.Authorize(recipientClient, recipientToken);
        var incoming = await recipientClient.GetAsync("/api/applications/incoming?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, incoming.StatusCode);
        var page = await incoming.Content.ReadFromJsonAsync<PagedResponse<IncomingApplicationListItem>>(
            _factory.JsonOptions);
        Assert.Contains(page!.Items, a => a.Id == application.Id);
        Assert.Equal("Quarterly review", page.Items.First(a => a.Id == application.Id).Title);
        Assert.Equal("Author", page.Items.First(a => a.Id == application.Id).AuthorName);
    }

    [Fact]
    public async Task Internal_play_forbidden_without_jwt()
    {
        var authorClient = _factory.CreateClient();
        var authorToken = await RegisterUserAsync(authorClient, "author2@test.com", "Author2");
        _factory.Authorize(authorClient, authorToken);

        await RegisterUserAsync(_factory.CreateClient(), "recipient2@test.com", "Recipient2");

        var search = await authorClient.GetAsync("/api/users/search?q=reci");
        var users = await search.Content.ReadFromJsonAsync<List<UserSearchResult>>(_factory.JsonOptions);
        var recipientId = users!.First(u => u.DisplayName == "Recipient2").Id;

        var test = await authorClient.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        var createdTest = await test.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions);

        var createApp = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Internal only",
            Type = ApplicationType.InternalUser,
            TestId = createdTest!.Id,
            RecipientUserId = recipientId
        });
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);

        var anonClient = _factory.CreateClient();
        var detail = await anonClient.GetAsync($"/api/applications/{application!.Token}");
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_invalid_application_rules()
    {
        var authorClient = _factory.CreateClient();
        var author = await RegisterUserAndReadAsync(authorClient, $"author-{Guid.NewGuid():N}@test.com", "Author");
        _factory.Authorize(authorClient, author.AccessToken);

        var privateTest = await CreatePrivateTestAsync(authorClient);
        var publicTest = await CreatePrivateTestAsync(authorClient);
        await authorClient.PutAsync($"/api/tests/{publicTest.Id}/publish", null);

        var recipientClient = _factory.CreateClient();
        var recipient = await RegisterUserAndReadAsync(recipientClient, $"recipient-{Guid.NewGuid():N}@test.com", "Recipient");

        var publicApplication = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Public",
            TestId = publicTest.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, publicApplication.StatusCode);

        var otherClient = _factory.CreateClient();
        var other = await RegisterUserAndReadAsync(otherClient, $"other-{Guid.NewGuid():N}@test.com", "Other");
        _factory.Authorize(otherClient, other.AccessToken);
        var foreignTest = await otherClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Foreign",
            TestId = privateTest.Id
        });
        Assert.Equal(HttpStatusCode.Forbidden, foreignTest.StatusCode);

        var internalWithoutRecipient = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "No recipient",
            Type = ApplicationType.InternalUser,
            TestId = privateTest.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, internalWithoutRecipient.StatusCode);

        var linkWithRecipient = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Link recipient",
            Type = ApplicationType.Link,
            TestId = privateTest.Id,
            RecipientUserId = recipient.User.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, linkWithRecipient.StatusCode);

        var selfRecipient = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Self",
            Type = ApplicationType.InternalUser,
            TestId = privateTest.Id,
            RecipientUserId = author.User.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, selfRecipient.StatusCode);
    }

    [Fact]
    public async Task Submit_after_completion_and_hidden_results_are_rejected()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client, $"submit-{Guid.NewGuid():N}@test.com");
        _factory.Authorize(client, token);

        var test = await CreatePrivateTestAsync(client);
        var createApp = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Complete",
            TestId = test.Id,
            HideResultsFromParticipant = true
        });
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync(
            $"/api/applications/{application!.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        await client.PostAsJsonAsync(
            $"/api/applications/{application.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 1, SelectedAnswerOrder = 0 });

        var afterCompletion = await client.PostAsJsonAsync(
            $"/api/applications/{application.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, afterCompletion.StatusCode);

        var hiddenResult = await client.GetAsync($"/api/applications/{application.Token}/result");
        Assert.Equal(HttpStatusCode.Forbidden, hiddenResult.StatusCode);
    }

    [Fact]
    public async Task Submit_rejects_reanswer_and_get_answers_returns_progress()
    {
        var client = _factory.CreateClient();
        var token = await _factory.RegisterAndGetTokenAsync(client, $"reanswer-{Guid.NewGuid():N}@test.com");
        _factory.Authorize(client, token);

        var test = await CreatePrivateTestAsync(client);
        var createApp = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Reanswer",
            TestId = test.Id
        });
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = null;
        var first = await client.PostAsJsonAsync(
            $"/api/applications/{application!.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var answers = await client.GetAsync($"/api/applications/{application.Token}/answers");
        Assert.Equal(HttpStatusCode.OK, answers.StatusCode);
        var answersBody = await answers.Content.ReadFromJsonAsync<AnsweredQuestionsResponse>(_factory.JsonOptions);
        Assert.Single(answersBody!.Answers);
        Assert.Equal(0, answersBody.Answers[0].QuestionOrder);

        var repeat = await client.PostAsJsonAsync(
            $"/api/applications/{application.Token}/submit",
            new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);
    }

    [Fact]
    public async Task Result_access_uses_application_token_and_internal_recipient()
    {
        var invalid = await _factory.CreateClient().GetAsync($"/api/applications/{Guid.NewGuid()}/result");
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);

        var authorClient = _factory.CreateClient();
        var author = await RegisterUserAndReadAsync(authorClient, $"access-author-{Guid.NewGuid():N}@test.com", "Access Author");
        _factory.Authorize(authorClient, author.AccessToken);

        var recipientClient = _factory.CreateClient();
        var recipient = await RegisterUserAndReadAsync(recipientClient, $"access-recipient-{Guid.NewGuid():N}@test.com", "Access Recipient");

        var otherClient = _factory.CreateClient();
        var other = await RegisterUserAndReadAsync(otherClient, $"access-other-{Guid.NewGuid():N}@test.com", "Access Other");

        var test = await CreatePrivateTestAsync(authorClient);
        var createApp = await authorClient.PostAsJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Title = "Internal result",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.User.Id
        });
        var application = await createApp.Content.ReadFromJsonAsync<ApplicationResponse>(_factory.JsonOptions);

        _factory.Authorize(recipientClient, recipient.AccessToken);
        var recipientResult = await recipientClient.GetAsync($"/api/applications/{application!.Token}/result");
        Assert.Equal(HttpStatusCode.OK, recipientResult.StatusCode);

        _factory.Authorize(otherClient, other.AccessToken);
        var otherResult = await otherClient.GetAsync($"/api/applications/{application.Token}/result");
        Assert.Equal(HttpStatusCode.Forbidden, otherResult.StatusCode);
    }

    private async Task<string> RegisterUserAsync(HttpClient client, string email, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = displayName
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions);
        return auth!.AccessToken;
    }

    private async Task<AuthResponse> RegisterUserAndReadAsync(HttpClient client, string email, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = displayName
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions))!;
    }

    private async Task<TestResponse> CreatePrivateTestAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/tests", ServiceFixture.SampleTestRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TestResponse>(_factory.JsonOptions))!;
    }
}
