using VibeTest.Server.Exceptions;
using VibeTest.Server.Models.Requests;

namespace VibeTest.Tests.Integration;

public class TestServiceTests
{
    [Fact]
    public async Task CreateTest_allows_duplicate_question_text_across_tests()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();

        var request = ServiceFixture.SampleTestRequest();
        var first = await fx.TestService.CreateTest(author.Id, request);
        var second = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Another test",
            Questions =
            [
                new QuestionInput
                {
                    Text = "What is SQL?",
                    Answers = ["Structured Query Language", "Wrong"],
                    Correct = 0
                }
            ]
        });

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, fx.Db.Questions.Count(q => q.Text == "What is SQL?"));
        Assert.Equal(1, fx.Db.Questions.Count(q => q.Text == "SELECT is?"));
    }

    [Fact]
    public async Task CreateTest_stores_question_explanation()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();

        var created = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "With explanation",
            Questions =
            [
                new QuestionInput
                {
                    Text = "Pick SQL",
                    Answers = ["Structured Query Language", "Wrong"],
                    Correct = 0,
                    Explanation = "SQL means Structured Query Language."
                }
            ]
        });

        var full = await fx.TestService.GetTestFull(created.Id, author.Id);
        Assert.Single(full.Questions);
        Assert.Equal("SQL means Structured Query Language.", full.Questions[0].Explanation);
    }

    [Fact]
    public async Task CreateTest_creates_separate_answer_rows_for_shared_text_within_single_test()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();

        var created = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Types quiz",
            Questions =
            [
                new QuestionInput
                {
                    Text = "First int question",
                    Answers = ["int", "double", "bool"],
                    Correct = 0
                },
                new QuestionInput
                {
                    Text = "Second int question",
                    Answers = ["byte", "int", "long"],
                    Correct = 1
                },
                new QuestionInput
                {
                    Text = "Third int question",
                    Answers = ["int?", "int", "Optional<int>"],
                    Correct = 1
                }
            ]
        });

        Assert.Equal(3, created.QuestionsCount);
        Assert.Equal(3, fx.Db.Answers.Count(a => a.Text == "int"));
    }

    [Fact]
    public async Task CreateTest_allows_duplicate_question_text_within_single_test()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();

        var created = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Repeated text",
            Questions =
            [
                new QuestionInput
                {
                    Text = "Same text",
                    Answers = ["A", "B"],
                    Correct = 0
                },
                new QuestionInput
                {
                    Text = "Same text",
                    Answers = ["C", "D"],
                    Correct = 1
                }
            ]
        });

        var full = await fx.TestService.GetTestFull(created.Id, author.Id);
        Assert.Equal(2, full.Questions.Count);
        Assert.Equal(2, fx.Db.Questions.Count(q => q.TestId == created.Id && q.Text == "Same text"));
    }

    [Fact]
    public async Task CreateTest_persists_questions_count()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();

        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        Assert.Equal(2, created.QuestionsCount);

        var test = await fx.Db.Tests.FindAsync(created.Id);
        Assert.NotNull(test);
        Assert.Equal(2, test!.QuestionsCount);
    }

    [Fact]
    public async Task AppendQuestions_increments_questions_count()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await fx.TestService.AppendQuestions(created.Id, author.Id, new AddQuestionsRequest
        {
            Questions =
            [
                new QuestionInput
                {
                    Text = "New question",
                    Answers = ["A", "B"],
                    Correct = 0
                }
            ]
        });

        var test = await fx.Db.Tests.FindAsync(created.Id);
        Assert.NotNull(test);
        Assert.Equal(3, test!.QuestionsCount);
    }

    [Fact]
    public async Task AppendQuestions_assigns_next_question_orders()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await fx.TestService.AppendQuestions(created.Id, author.Id, new AddQuestionsRequest
        {
            Questions =
            [
                new QuestionInput
                {
                    Text = "New question",
                    Answers = ["A", "B"],
                    Correct = 0
                }
            ]
        });

        var full = await fx.TestService.GetTestFull(created.Id, author.Id);
        Assert.Equal(3, full.Questions.Count);
        Assert.Equal(2, full.Questions.Last().Order);
        Assert.Equal("New question", full.Questions.Last().Text);
    }

    [Fact]
    public async Task AppendQuestions_forbidden_for_non_author()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var other = await fx.SeedUserAsync("bob@test.com", "Bob");
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.TestService.AppendQuestions(created.Id, other.Id, new AddQuestionsRequest
            {
                Questions = ServiceFixture.SampleTestRequest().Questions
            }));
    }

    [Fact]
    public async Task GetTestDetail_hides_correct_answers_and_private_tests()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<NotFoundException>(() => fx.TestService.GetTestDetail(created.Id));

        var authorDetail = await fx.TestService.GetTestDetail(created.Id, author.Id);
        Assert.Equal(2, authorDetail.Questions.Count);

        await fx.TestService.PublishTest(created.Id, author.Id);
        var detail = await fx.TestService.GetTestDetail(created.Id);

        Assert.Equal(2, detail.Questions.Count);
        Assert.All(detail.Questions.SelectMany(q => q.Answers), a =>
        {
            Assert.False(a.GetType().GetProperty("IsCorrect")?.GetValue(a) is true);
        });
    }

    [Fact]
    public async Task GetTestFull_includes_correct_flags_for_author()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var full = await fx.TestService.GetTestFull(created.Id, author.Id);
        Assert.Equal(0, full.Questions[0].Correct);
    }

    [Fact]
    public async Task GetPublicTests_returns_published_tests_only()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var privateTest = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var publicTest = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Public",
            Questions = ServiceFixture.SampleTestRequest().Questions
        });
        await fx.TestService.PublishTest(publicTest.Id, author.Id);

        var page = await fx.TestService.GetPublicTests(1, 10, "updatedAt", "desc");

        Assert.Single(page.Items);
        Assert.Equal(publicTest.Id, page.Items[0].Id);
        Assert.DoesNotContain(page.Items, i => i.Id == privateTest.Id);
    }

    [Fact]
    public async Task GetPublicPlayTest_returns_full_payload_for_published_test()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(created.Id, author.Id);

        var play = await fx.TestService.GetPublicPlayTest(created.Id);

        Assert.Equal(created.Id, play.Id);
        Assert.Equal(2, play.Questions.Count);
        Assert.Equal(0, play.Questions[0].Correct);
    }

    [Fact]
    public async Task GetPublicPlayTest_rejects_private_test()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        await Assert.ThrowsAsync<NotFoundException>(() => fx.TestService.GetPublicPlayTest(created.Id));
    }

    [Fact]
    public async Task DeleteTest_cascades_questions_and_answers()
    {
        using var fx = new ServiceFixture();
        var author = await fx.SeedUserAsync();
        var created = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());

        var questionIds = fx.Db.Questions.Where(q => q.TestId == created.Id).Select(q => q.Id).ToList();
        Assert.NotEmpty(questionIds);

        await fx.TestService.DeleteTest(created.Id, author.Id);

        Assert.Empty(fx.Db.Questions.Where(q => q.TestId == created.Id));
        Assert.Empty(fx.Db.Answers.Where(a => questionIds.Contains(a.QuestionId)));
    }
}
