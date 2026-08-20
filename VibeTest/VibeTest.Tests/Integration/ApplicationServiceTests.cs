using VibeTest.Server.Exceptions;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Requests;

namespace VibeTest.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public class ApplicationServiceTests(PostgreSqlTestFixture postgres)
{
    [Fact]
    public async Task CreateApplication_link_type_sets_title_no_recipient()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Bob",
            Type = ApplicationType.Link,
            TestId = test.Id
        });

        Assert.NotEqual(Guid.Empty, app.Token);
        Assert.Equal("Bob", app.Title);
        Assert.Equal(ApplicationType.Link, app.Type);
        Assert.Equal(test.Id, app.TestId);
        Assert.Equal("SQL Basics", app.TestName);
        Assert.Contains(app.Token.ToString(), app.PlayUrl);
    }

    [Fact]
    public async Task CreateApplication_succeeds_for_private_own_test()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Bob",
            TestId = test.Id
        });

        Assert.NotEqual(Guid.Empty, app.Token);
        Assert.Equal("Bob", app.Title);
        Assert.Equal(ApplicationType.Link, app.Type);
    }

    [Fact]
    public async Task CreateApplication_rejects_public_test()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
            {
                Title = "Bob",
                TestId = test.Id
            }));
    }

    [Fact]
    public async Task CreateApplication_rejects_foreign_test()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var other = await fx.SeedUserAsync("other@test.com", "Other");
        var test = await fx.TestService.CreateTest(other.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
            {
                Title = "Bob",
                TestId = test.Id
            }));
    }

    [Fact]
    public async Task CreateApplication_internal_requires_recipient()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
            {
                Title = "Internal",
                Type = ApplicationType.InternalUser,
                TestId = test.Id
            }));
    }

    [Fact]
    public async Task CreateApplication_link_rejects_recipientUserId()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
            {
                Title = "Link",
                Type = ApplicationType.Link,
                TestId = test.Id,
                RecipientUserId = recipient.Id
            }));
    }

    [Fact]
    public async Task SubmitAnswer_and_result_work_via_token()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Carol",
            TestId = test.Id
        });

        var wrong = await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        }, null);
        Assert.Equal(0, wrong.CorrectAnswerOrder);

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }, null));

        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 0
        }, null);

        var result = await fx.ApplicationService.GetApplicationResult(app.Token, null);
        Assert.Equal(1, result.CorrectAnswers);
        Assert.Equal(1, result.IncorrectAnswers);
        Assert.Equal(2, result.TotalQuestions);
        Assert.NotNull(result.CompletedAt);

        var list = await fx.ApplicationService.GetMyApplications(author.Id, 1, 10);
        Assert.Single(list.Items);
        Assert.True(list.Items[0].IsCompleted);
        Assert.Equal(50, list.Items[0].ScorePercent);
    }

    [Fact]
    public async Task GetApplicationPlayDetail_returns_test_for_anonymous_token()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Dave",
            TestId = test.Id
        });

        var detail = await fx.ApplicationService.GetApplicationPlayDetail(app.Token, null);
        Assert.Equal(test.Id, detail.Id);
        Assert.Equal(2, detail.Questions.Count);
        Assert.Equal("Dave", detail.Title);
        Assert.False(detail.HideResultsFromParticipant);
        Assert.False(detail.IsCompleted);
    }

    [Fact]
    public async Task CreateApplication_with_hideResults_flag()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Frank",
            TestId = test.Id,
            HideResultsFromParticipant = true
        });

        Assert.True(app.HideResultsFromParticipant);

        var list = await fx.ApplicationService.GetMyApplications(author.Id, 1, 10);
        Assert.True(list.Items[0].HideResultsFromParticipant);
    }

    [Fact]
    public async Task SubmitAnswer_rejects_reanswer_before_completion()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "No reanswer",
            TestId = test.Id
        });

        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        }, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }, null));

        Assert.Equal("На этот вопрос уже дан ответ", ex.Message);
    }

    [Fact]
    public async Task GetApplicationAnsweredQuestions_returns_saved_answers()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Answers",
            TestId = test.Id
        });

        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        }, null);

        var answers = await fx.ApplicationService.GetApplicationAnsweredQuestions(app.Token, null);

        Assert.Single(answers.Answers);
        Assert.Equal(0, answers.Answers[0].QuestionOrder);
        Assert.True(answers.Answers[0].IsCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_after_completed_throws()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Grace",
            TestId = test.Id
        });

        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        }, null);
        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 0
        }, null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }, null));
    }

    [Fact]
    public async Task SubmitAnswer_hideResults_omits_feedback()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Helen",
            TestId = test.Id,
            HideResultsFromParticipant = true
        });

        var response = await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        }, null);

        Assert.Equal(-1, response.CorrectAnswerOrder);
        Assert.Null(response.Explanation);
    }

    [Fact]
    public async Task GetApplicationResult_hideResults_forbidden()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Ivan",
            TestId = test.Id,
            HideResultsFromParticipant = true
        });

        await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        }, null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.GetApplicationResult(app.Token, null));
    }

    [Fact]
    public async Task CreateApplication_internal_sets_type_and_recipient()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Quarterly check",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.Id
        });

        Assert.Equal("Quarterly check", app.Title);
        Assert.Equal(ApplicationType.InternalUser, app.Type);
        Assert.Equal(string.Empty, app.PlayUrl);

        var list = await fx.ApplicationService.GetMyApplications(author.Id, 1, 10);
        Assert.Equal(recipient.Id, list.Items[0].RecipientUserId);
        Assert.Equal(ApplicationType.InternalUser, list.Items[0].Type);
        Assert.Equal(string.Empty, list.Items[0].PlayUrl);
    }

    [Fact]
    public async Task Author_list_internal_has_empty_playUrl()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Internal app",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.Id
        });

        var list = await fx.ApplicationService.GetMyApplications(author.Id, 1, 10);
        Assert.Single(list.Items);
        Assert.Equal(string.Empty, list.Items[0].PlayUrl);
    }

    [Fact]
    public async Task Internal_play_forbidden_anonymous_and_wrong_user()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var other = await fx.SeedUserAsync("carol@test.com", "Carol");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Internal",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.Id
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.GetApplicationPlayDetail(app.Token, null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.GetApplicationPlayDetail(app.Token, other.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }, null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.ApplicationService.GetApplicationResult(app.Token, null));
    }

    [Fact]
    public async Task Internal_play_allowed_for_recipient_with_jwt()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var app = await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "Internal",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.Id
        });

        var detail = await fx.ApplicationService.GetApplicationPlayDetail(app.Token, recipient.Id);
        Assert.Equal("Internal", detail.Title);
        Assert.Equal(test.Id, detail.Id);

        var submit = await fx.ApplicationService.SubmitAnswer(app.Token, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        }, recipient.Id);
        Assert.Equal(0, submit.CorrectAnswerOrder);
    }

    [Fact]
    public async Task CreateApplication_rejects_self_as_recipient()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<ValidationException>(() =>
            fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
            {
                Title = "Self",
                Type = ApplicationType.InternalUser,
                TestId = test.Id,
                RecipientUserId = author.Id
            }));
    }

    [Fact]
    public async Task GetIncoming_returns_only_for_recipient()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var recipient = await fx.SeedUserAsync("bob@test.com", "Bob");
        var other = await fx.SeedUserAsync("carol@test.com", "Carol");
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "For Bob",
            Type = ApplicationType.InternalUser,
            TestId = test.Id,
            RecipientUserId = recipient.Id
        });
        await fx.ApplicationService.CreateApplication(author.Id, new CreateApplicationRequest
        {
            Title = "External",
            TestId = test.Id
        });

        var incoming = await fx.ApplicationService.GetIncomingApplications(recipient.Id, 1, 10);
        Assert.Single(incoming.Items);
        Assert.Equal("Alice", incoming.Items[0].AuthorName);
        Assert.Equal("For Bob", incoming.Items[0].Title);

        var otherIncoming = await fx.ApplicationService.GetIncomingApplications(other.Id, 1, 10);
        Assert.Empty(otherIncoming.Items);
    }

    [Fact]
    public async Task UserSearch_excludes_self()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        await fx.SeedUserAsync("bob@test.com", "Bob");

        var results = await fx.UserRepository.SearchAsync("bob", author.Id, 10);
        Assert.Single(results);
        Assert.Equal("Bob", results[0].DisplayName);
        Assert.DoesNotContain(results, r => r.Id == author.Id);
    }
}
