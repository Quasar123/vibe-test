using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Models.Requests;

namespace VibeTest.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public class ResultServiceTests(PostgreSqlTestFixture postgres)
{
    [Fact]
    public async Task SubmitAnswer_updates_user_test_result_aggregate()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });

        var aggregate = await fx.Db.UserTestResults
            .FirstOrDefaultAsync(utr => utr.UserId == author.Id && utr.TestId == test.Id);

        Assert.NotNull(aggregate);
        Assert.Equal(1, aggregate!.CorrectAnswer);
        Assert.Equal(0, aggregate.IncorrectAnswer);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 1
        });

        await fx.Db.Entry(aggregate).ReloadAsync();
        Assert.Equal(1, aggregate.CorrectAnswer);
        Assert.Equal(1, aggregate.IncorrectAnswer);
    }

    [Fact]
    public async Task DeleteResult_clears_user_test_result_aggregate()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });

        Assert.NotEmpty(fx.Db.UserTestResults.Where(utr => utr.UserId == author.Id && utr.TestId == test.Id));

        await fx.ResultService.DeleteResult(author.Id, test.Id);

        Assert.Empty(fx.Db.UserTestResults.Where(utr => utr.UserId == author.Id && utr.TestId == test.Id));
    }

    [Fact]
    public async Task AppendQuestions_keeps_existing_aggregate_and_updates_total_questions()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 1, SelectedAnswerOrder = 0 });

        await fx.TestService.AppendQuestions(test.Id, author.Id, new AddQuestionsRequest
        {
            Questions =
            [
                new QuestionInput
                {
                    Text = "Extra",
                    Answers = ["Yes", "No"],
                    Correct = 0
                }
            ]
        });

        var aggregate = await fx.Db.UserTestResults
            .FirstAsync(utr => utr.UserId == author.Id && utr.TestId == test.Id);
        var updatedTest = await fx.Db.Tests.FindAsync(test.Id);

        Assert.Equal(2, aggregate.CorrectAnswer);
        Assert.Equal(0, aggregate.IncorrectAnswer);
        Assert.Equal(3, updatedTest!.QuestionsCount);

        var afterAppend = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(3, afterAppend.TotalQuestions);
        Assert.Equal(2, afterAppend.CorrectAnswers);
        Assert.Equal(0, afterAppend.IncorrectAnswers);
    }

    [Fact]
    public async Task SubmitAnswer_returns_explanation_when_present()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Explained",
            Questions =
            [
                new QuestionInput
                {
                    Text = "Pick SQL",
                    Answers = ["Structured Query Language", "Wrong"],
                    Correct = 0,
                    Explanation = "SQL expands to Structured Query Language."
                }
            ]
        });
        await fx.TestService.PublishTest(test.Id, author.Id);

        var wrong = await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        });
        Assert.Equal(0, wrong.CorrectAnswerOrder);
        Assert.Equal("SQL expands to Structured Query Language.", wrong.Explanation);
    }

    [Fact]
    public async Task SubmitAnswer_rejects_reanswer_on_same_question()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        });

        var ex = await Assert.ThrowsAsync<VibeTest.Server.Exceptions.ValidationException>(() =>
            fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }));

        Assert.Equal("На этот вопрос уже дан ответ", ex.Message);

        var result = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(1, result.IncorrectAnswers);
        Assert.Equal(0, result.CorrectAnswers);
    }

    [Fact]
    public async Task GetAnsweredQuestions_returns_saved_answers()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        });

        var answers = await fx.ResultService.GetAnsweredQuestions(author.Id, test.Id);

        Assert.Single(answers.Answers);
        Assert.Equal(0, answers.Answers[0].QuestionOrder);
        Assert.Equal(1, answers.Answers[0].SelectedAnswerOrder);
        Assert.Equal(0, answers.Answers[0].CorrectAnswerOrder);
        Assert.False(answers.Answers[0].IsCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_returns_correct_order_without_reanswer()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        var wrong = await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 1
        });
        Assert.Equal(0, wrong.CorrectAnswerOrder);

        var result = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(1, result.IncorrectAnswers);
        Assert.Equal(0, result.CorrectAnswers);

        await Assert.ThrowsAsync<VibeTest.Server.Exceptions.ValidationException>(() =>
            fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
            {
                QuestionOrder = 0,
                SelectedAnswerOrder = 0
            }));

        result = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(1, result.IncorrectAnswers);
        Assert.Equal(0, result.CorrectAnswers);
    }

    [Fact]
    public async Task AppendQuestions_allows_resolving_new_questions()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 1, SelectedAnswerOrder = 0 });

        var complete = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(2, complete.TotalQuestions);
        Assert.Equal(2, complete.CorrectAnswers);

        await fx.TestService.AppendQuestions(test.Id, author.Id, new AddQuestionsRequest
        {
            Questions =
            [
                new QuestionInput
                {
                    Text = "Extra",
                    Answers = ["Yes", "No"],
                    Correct = 0
                }
            ]
        });

        var afterAppend = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(3, afterAppend.TotalQuestions);
        Assert.Equal(2, afterAppend.CorrectAnswers);
        Assert.Equal(0, afterAppend.IncorrectAnswers);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 2, SelectedAnswerOrder = 0 });

        afterAppend = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(3, afterAppend.CorrectAnswers);
        Assert.Equal(0, afterAppend.IncorrectAnswers);
    }

    [Fact]
    public async Task DeleteResult_clears_attempt_for_retake()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        await fx.ResultService.DeleteResult(author.Id, test.Id);

        var result = await fx.ResultService.GetResult(author.Id, test.Id);
        Assert.Equal(0, result.CorrectAnswers);
        Assert.Equal(0, result.IncorrectAnswers);
        Assert.Null(result.StartedAt);
    }

    [Fact]
    public async Task GetUserResults_returns_history()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 0, SelectedAnswerOrder = 0 });
        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest { QuestionOrder = 1, SelectedAnswerOrder = 0 });

        var history = await fx.ResultService.GetUserResults(author.Id, 1, 10, "date", "desc");

        Assert.Single(history.Items);
        Assert.Equal(test.Id, history.Items[0].TestId);
        Assert.Equal(100, history.Items[0].ScorePercent);
    }

    [Fact]
    public async Task GetUserTestProgress_returns_bulk_progress_for_requested_tests()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var test = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(test.Id, author.Id);
        var untouched = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Untouched",
            Questions = ServiceFixture.SampleTestRequest().Questions
        });
        await fx.TestService.PublishTest(untouched.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });
        await fx.ResultService.SubmitAnswer(author.Id, test.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 1
        });

        var progress = await fx.ResultService.GetUserTestProgress(
            author.Id,
            [test.Id, untouched.Id, 99999]);

        Assert.Equal(2, progress.Items.Count);

        var started = progress.Items.Single(item => item.TestId == test.Id);
        Assert.Equal(2, started.AnsweredCount);
        Assert.Equal(1, started.CorrectCount);
        Assert.Equal(1, started.IncorrectCount);
        Assert.NotNull(started.StartedAt);
        Assert.NotNull(started.CompletedAt);

        var empty = progress.Items.Single(item => item.TestId == untouched.Id);
        Assert.Equal(0, empty.AnsweredCount);
        Assert.Equal(0, empty.CorrectCount);
        Assert.Equal(0, empty.IncorrectCount);
    }

    [Fact]
    public async Task GetUserTestProgress_returns_empty_for_no_ids()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();

        var progress = await fx.ResultService.GetUserTestProgress(author.Id, []);

        Assert.Empty(progress.Items);
    }
}
