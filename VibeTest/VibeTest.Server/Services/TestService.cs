using VibeTest.Server.Data.Repositories;
using VibeTest.Server.Exceptions;
using VibeTest.Server.Helpers;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Services;

public class TestService(
    ITestRepository tests,
    ILogger<TestService> logger) : ITestService
{
    public async Task<PagedResponse<TestListItem>> GetPublicTests(int page, int pageSize, string sortBy, string order)
    {
        logger.LogDebug(
            "GetPublicTests page={Page} pageSize={PageSize} sortBy={SortBy} order={Order}",
            page,
            pageSize,
            sortBy,
            order);

        var (normalizedPage, normalizedSize, offset) = NormalizePagination(page, pageSize);
        var (normalizedSort, normalizedOrder) = NormalizeTestListSort(sortBy, order);
        var totalCount = await tests.CountPublicTestsAsync();
        var rows = await tests.GetPublicTestsPageAsync(offset, normalizedSize, normalizedSort, normalizedOrder);
        var items = rows.Select(MapListItem).ToList();

        logger.LogInformation("Public tests listed: page={Page} total={TotalCount}", normalizedPage, totalCount);

        return PaginationHelper.Create(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<TestDetailResponse> GetTestDetail(int testId, int? viewerUserId = null)
    {
        logger.LogDebug("GetTestDetail test={TestId} viewer={ViewerUserId}", testId, viewerUserId);

        var test = await tests.GetByIdWithStructureAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        if (!test.IsPublic && test.AuthorId != viewerUserId)
            throw new NotFoundException("Тест не найден");

        return MapDetail(test);
    }

    public async Task<TestResponse> CreateTest(int authorId, CreateTestRequest request)
    {
        logger.LogDebug("CreateTest author={AuthorId} name={Name}", authorId, request.Name);

        TestRequestValidator.ValidateCreateTest(request);

        var now = DateTime.UtcNow;
        var test = new Test
        {
            AuthorId = authorId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsPublic = false,
            Difficulty = request.Difficulty ?? TestDifficulty.Easy,
            QuestionsCount = request.Questions.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        AddQuestions(test, request.Questions, startQuestionOrder: 0);
        await tests.AddAsync(test);
        await tests.SaveChangesAsync();

        logger.LogInformation(
            "Test created: {TestId} by author {AuthorId} with {QuestionCount} questions",
            test.Id,
            authorId,
            request.Questions.Count);

        return MapResponse(test);
    }

    public async Task<TestResponse> AppendQuestions(int testId, int authorId, AddQuestionsRequest request)
    {
        logger.LogDebug(
            "AppendQuestions test={TestId} author={AuthorId} count={Count}",
            testId,
            authorId,
            request.Questions.Count);

        TestRequestValidator.ValidateQuestions(request.Questions);

        var test = await tests.GetByIdWithStructureAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        var maxOrder = await tests.GetMaxQuestionOrderAsync(testId);
        AddQuestions(test, request.Questions, startQuestionOrder: maxOrder + 1);
        test.QuestionsCount += request.Questions.Count;
        test.UpdatedAt = DateTime.UtcNow;
        await tests.SaveChangesAsync();

        logger.LogInformation(
            "Questions appended to test {TestId}: {Count} questions",
            testId,
            request.Questions.Count);

        return MapResponse(test);
    }

    public async Task<TestResponse> UpdateTestInfo(int testId, int authorId, UpdateTestInfoRequest request)
    {
        logger.LogDebug("UpdateTestInfo test={TestId} author={AuthorId}", testId, authorId);

        TestRequestValidator.ValidateUpdateInfo(request);

        var test = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        test.Name = request.Name.Trim();
        test.Description = request.Description?.Trim();
        if (request.Difficulty.HasValue)
            test.Difficulty = request.Difficulty.Value;
        test.UpdatedAt = DateTime.UtcNow;
        await tests.SaveChangesAsync();

        logger.LogInformation("Test info updated: {TestId}", testId);

        return MapResponse(test);
    }

    public async Task PublishTest(int testId, int authorId)
    {
        logger.LogDebug("PublishTest test={TestId} author={AuthorId}", testId, authorId);

        var test = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        test.IsPublic = true;
        test.UpdatedAt = DateTime.UtcNow;
        await tests.SaveChangesAsync();

        logger.LogInformation("Test published: {TestId}", testId);
    }

    public async Task UnpublishTest(int testId, int authorId)
    {
        logger.LogDebug("UnpublishTest test={TestId} author={AuthorId}", testId, authorId);

        var test = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        test.IsPublic = false;
        test.UpdatedAt = DateTime.UtcNow;
        await tests.SaveChangesAsync();

        logger.LogInformation("Test unpublished: {TestId}", testId);
    }

    public async Task DeleteTest(int testId, int authorId)
    {
        logger.LogDebug("DeleteTest test={TestId} author={AuthorId}", testId, authorId);

        var test = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        await tests.DeleteAsync(test);
        await tests.SaveChangesAsync();

        logger.LogInformation("Test deleted: {TestId}", testId);
    }

    public async Task<PagedResponse<TestListItem>> GetMyTests(
        int authorId,
        int page,
        int pageSize,
        string filter,
        string sortBy,
        string order)
    {
        logger.LogDebug(
            "GetMyTests author={AuthorId} page={Page} filter={Filter}",
            authorId,
            page,
            filter);

        var normalizedFilter = NormalizeFilter(filter);
        var (normalizedPage, normalizedSize, offset) = NormalizePagination(page, pageSize);
        var (normalizedSort, normalizedOrder) = NormalizeTestListSort(sortBy, order);
        var totalCount = await tests.CountMyTestsAsync(authorId, normalizedFilter);
        var rows = await tests.GetMyTestsPageAsync(
            authorId,
            normalizedFilter,
            offset,
            normalizedSize,
            normalizedSort,
            normalizedOrder);
        var items = rows.Select(MapListItem).ToList();

        logger.LogInformation(
            "My tests listed for author {AuthorId}: page={Page} total={TotalCount}",
            authorId,
            normalizedPage,
            totalCount);

        return PaginationHelper.Create(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<TestFullResponse> GetTestFull(int testId, int authorId)
    {
        logger.LogDebug("GetTestFull test={TestId} author={AuthorId}", testId, authorId);

        var test = await tests.GetByIdWithStructureAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        EnsureAuthor(test, authorId);

        return new TestFullResponse
        {
            Id = test.Id,
            Name = test.Name,
            Description = test.Description,
            IsPublic = test.IsPublic,
            Difficulty = test.Difficulty,
            Questions = QuestionMapper.ToFullQuestions(test.Questions)
        };
    }

    public async Task<TestFullResponse> GetPublicPlayTest(int testId)
    {
        logger.LogDebug("GetPublicPlayTest test={TestId}", testId);

        var test = await tests.GetByIdWithStructureAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        if (!test.IsPublic)
            throw new NotFoundException("Тест не найден");

        return new TestFullResponse
        {
            Id = test.Id,
            Name = test.Name,
            Description = test.Description,
            IsPublic = test.IsPublic,
            Difficulty = test.Difficulty,
            Questions = QuestionMapper.ToFullQuestions(test.Questions)
        };
    }

    private static void AddQuestions(Test test, IReadOnlyList<QuestionInput> questions, int startQuestionOrder)
    {
        for (var qi = 0; qi < questions.Count; qi++)
        {
            var questionInput = questions[qi];
            var explanation = string.IsNullOrWhiteSpace(questionInput.Explanation)
                ? null
                : questionInput.Explanation.Trim();

            var question = new Question
            {
                Test = test,
                Text = questionInput.Text.Trim(),
                Order = startQuestionOrder + qi,
                Explanation = explanation
            };

            for (var ai = 0; ai < questionInput.Answers.Count; ai++)
            {
                question.Answers.Add(new Answer
                {
                    Text = questionInput.Answers[ai].Trim(),
                    Order = ai,
                    IsCorrect = ai == questionInput.Correct
                });
            }

            test.Questions.Add(question);
        }
    }

    private static void EnsureAuthor(Test test, int authorId)
    {
        if (test.AuthorId != authorId)
            throw new ForbiddenException("Доступ запрещён");
    }

    private static (int Page, int PageSize, int Offset) NormalizePagination(int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        return (normalizedPage, normalizedSize, (normalizedPage - 1) * normalizedSize);
    }

    private static string NormalizeFilter(string filter) =>
        filter switch
        {
            "published" => "published",
            "private" => "private",
            _ => "all"
        };

    private static (string SortBy, string Order) NormalizeTestListSort(string sortBy, string order)
    {
        var normalizedSort = sortBy == "name" ? "name" : "updatedAt";
        var normalizedOrder = order == "asc" ? "asc" : "desc";
        return (normalizedSort, normalizedOrder);
    }

    private static TestListItem MapListItem(Data.Queries.TestListItemRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Description = row.Description,
        AuthorName = row.AuthorName,
        QuestionsCount = row.QuestionsCount,
        IsPublic = row.IsPublic,
        Difficulty = row.Difficulty,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };

    private static TestDetailResponse MapDetail(Test test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Description = test.Description,
        AuthorName = test.Author.DisplayName,
        Questions = QuestionMapper.ToDetailQuestions(test.Questions)
    };

    private static TestResponse MapResponse(Test test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Description = test.Description,
        IsPublic = test.IsPublic,
        Difficulty = test.Difficulty,
        QuestionsCount = test.QuestionsCount,
        CreatedAt = test.CreatedAt
    };
}
