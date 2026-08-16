using VibeTest.Server.Models.Requests;

namespace VibeTest.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public class UserServiceTests(PostgreSqlTestFixture postgres)
{
    [Fact]
    public async Task GetStats_calculates_created_published_passed_and_average()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();

        var privateTest = await fx.TestService.CreateTest(author.Id, ServiceFixture.SampleTestRequest());
        var publicTest = await fx.TestService.CreateTest(author.Id, new CreateTestRequest
        {
            Name = "Public",
            Questions = ServiceFixture.SampleTestRequest().Questions
        });
        await fx.TestService.PublishTest(publicTest.Id, author.Id);

        await fx.ResultService.SubmitAnswer(author.Id, publicTest.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });
        await fx.ResultService.SubmitAnswer(author.Id, publicTest.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 1
        });

        var stats = await fx.UserService.GetStats(author.Id);

        Assert.Equal(2, stats.TotalCreated);
        Assert.Equal(1, stats.TotalPublished);
        Assert.Equal(1, stats.TotalPassedOwn);
        Assert.Equal(50, stats.AverageScoreOwn);
        Assert.Equal(0, stats.TotalPassedOthers);
        Assert.Equal(0, stats.AverageScoreOthers);
        Assert.NotEqual(privateTest.Id, publicTest.Id);
    }

    [Fact]
    public async Task GetStats_counts_passing_others_tests_separately()
    {
        using var fx = new ServiceFixture(postgres);
        var author = await fx.SeedUserAsync();
        var other = await fx.SeedUserAsync("bob@test.com", "Bob");

        var othersTest = await fx.TestService.CreateTest(other.Id, ServiceFixture.SampleTestRequest());
        await fx.TestService.PublishTest(othersTest.Id, other.Id);

        await fx.ResultService.SubmitAnswer(author.Id, othersTest.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 0,
            SelectedAnswerOrder = 0
        });
        await fx.ResultService.SubmitAnswer(author.Id, othersTest.Id, new SubmitAnswerRequest
        {
            QuestionOrder = 1,
            SelectedAnswerOrder = 1
        });

        var stats = await fx.UserService.GetStats(author.Id);

        Assert.Equal(0, stats.TotalPassedOwn);
        Assert.Equal(0, stats.AverageScoreOwn);
        Assert.Equal(1, stats.TotalPassedOthers);
        Assert.Equal(50, stats.AverageScoreOthers);
    }

    [Fact]
    public async Task GetStats_returns_zeros_for_new_user()
    {
        using var fx = new ServiceFixture(postgres);
        var user = await fx.SeedUserAsync("new@test.com", "New");

        var stats = await fx.UserService.GetStats(user.Id);

        Assert.Equal(0, stats.TotalCreated);
        Assert.Equal(0, stats.TotalPublished);
        Assert.Equal(0, stats.TotalPassedOwn);
        Assert.Equal(0, stats.TotalPassedOthers);
        Assert.Equal(0, stats.AverageScoreOwn);
        Assert.Equal(0, stats.AverageScoreOthers);
    }
}
