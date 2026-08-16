using VibeTest.Server.Data.Repositories;
using VibeTest.Server.Exceptions;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Services;

public class ResultService(
    ITestRepository tests,
    IResultRepository results,
    IUserRepository users,
    ILogger<ResultService> logger) : IResultService
{
    public async Task<SubmitResponse> SubmitAnswer(int userId, int testId, SubmitAnswerRequest request)
    {
        logger.LogDebug(
            "SubmitAnswer user={UserId} test={TestId} question={QuestionOrder} answer={AnswerOrder}",
            userId,
            testId,
            request.QuestionOrder,
            request.SelectedAnswerOrder);

        if (!await users.ExistsAsync(userId))
            throw new NotFoundException("Пользователь не найден");

        var test = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        var selected = await results.GetAnswerByOrdersAsync(testId, request.QuestionOrder, request.SelectedAnswerOrder)
            ?? throw new ValidationException("Неверный вопрос или ответ");

        var correctOrder = await results.GetCorrectAnswerOrderAsync(testId, request.QuestionOrder)
            ?? throw new ValidationException("Для вопроса не задан правильный ответ");

        if (await results.HasAnswerForQuestionAsync(userId, testId, selected.QuestionId))
            throw new ValidationException("На этот вопрос уже дан ответ");

        await results.InsertAsync(new Result
        {
            UserId = userId,
            TestId = testId,
            QuestionId = selected.QuestionId,
            AnswerId = selected.Id,
            AnsweredAt = DateTime.UtcNow
        });

        var isCorrect = request.SelectedAnswerOrder == correctOrder;
        var aggregate = await results.GetUserTestResultAsync(userId, testId);
        if (aggregate is null)
        {
            await results.InsertUserTestResultAsync(new UserTestResult
            {
                UserId = userId,
                TestId = testId,
                CorrectAnswer = isCorrect ? 1 : 0,
                IncorrectAnswer = isCorrect ? 0 : 1
            });
        }
        else if (isCorrect)
        {
            aggregate.CorrectAnswer++;
        }
        else
        {
            aggregate.IncorrectAnswer++;
        }

        await results.SaveChangesAsync();

        logger.LogInformation(
            "Answer submitted user={UserId} test={TestId} question={QuestionOrder} correct={IsCorrect}",
            userId,
            testId,
            request.QuestionOrder,
            isCorrect);

        var explanation = await results.GetQuestionExplanationAsync(testId, request.QuestionOrder);

        return new SubmitResponse { CorrectAnswerOrder = correctOrder, Explanation = explanation };
    }

    public async Task<TestResultResponse> GetResult(int userId, int testId)
    {
        logger.LogDebug("GetResult user={UserId} test={TestId}", userId, testId);

        var summary = await results.GetTestResultSummaryAsync(userId, testId)
            ?? throw new NotFoundException("Тест не найден");

        logger.LogInformation(
            "Result retrieved user={UserId} test={TestId} score={Correct}/{Total}",
            userId,
            testId,
            summary.CorrectAnswers,
            summary.TotalQuestions);

        return new TestResultResponse
        {
            TestId = summary.TestId,
            TestName = summary.TestName,
            TotalQuestions = summary.TotalQuestions,
            CorrectAnswers = summary.CorrectAnswers,
            IncorrectAnswers = summary.IncorrectAnswers,
            StartedAt = summary.StartedAt,
            CompletedAt = summary.CompletedAt
        };
    }

    public async Task DeleteResult(int userId, int testId)
    {
        logger.LogDebug("DeleteResult user={UserId} test={TestId}", userId, testId);

        _ = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        await results.DeleteByUserAndTestAsync(userId, testId);
        await results.DeleteUserTestResultAsync(userId, testId);
        await results.SaveChangesAsync();

        logger.LogInformation("Result deleted user={UserId} test={TestId}", userId, testId);
    }

    public async Task<PagedResponse<TestHistoryItem>> GetUserResults(
        int userId,
        int page,
        int pageSize,
        string sortBy,
        string order)
    {
        logger.LogDebug(
            "GetUserResults user={UserId} page={Page} pageSize={PageSize} sortBy={SortBy} order={Order}",
            userId,
            page,
            pageSize,
            sortBy,
            order);

        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var offset = (normalizedPage - 1) * normalizedSize;
        var normalizedSort = sortBy == "score" ? "score" : "date";
        var normalizedOrder = order == "asc" ? "asc" : "desc";

        var totalCount = await results.CountUserHistoryAsync(userId);
        var rows = await results.GetUserHistoryPageAsync(userId, normalizedSort, normalizedOrder, offset, normalizedSize);

        var items = rows.Select(row => new TestHistoryItem
        {
            TestId = row.TestId,
            TestName = row.TestName,
            TotalQuestions = row.TotalQuestions,
            CorrectAnswers = row.CorrectAnswers,
            ScorePercent = row.ScorePercent,
            CompletedAt = row.CompletedAt
        }).ToList();

        logger.LogInformation(
            "User history retrieved user={UserId} page={Page} total={TotalCount}",
            userId,
            normalizedPage,
            totalCount);

        return Helpers.PaginationHelper.Create(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<AnsweredQuestionsResponse> GetAnsweredQuestions(int userId, int testId)
    {
        logger.LogDebug("GetAnsweredQuestions user={UserId} test={TestId}", userId, testId);

        _ = await tests.GetByIdAsync(testId)
            ?? throw new NotFoundException("Тест не найден");

        var rows = await results.GetAnsweredQuestionsAsync(userId, testId);

        return new AnsweredQuestionsResponse
        {
            Answers = rows.Select(row => new AnsweredQuestionResponse
            {
                QuestionOrder = row.QuestionOrder,
                SelectedAnswerOrder = row.SelectedAnswerOrder,
                CorrectAnswerOrder = row.CorrectAnswerOrder,
                IsCorrect = row.IsCorrect,
                Explanation = string.IsNullOrWhiteSpace(row.Explanation) ? null : row.Explanation
            }).ToList()
        };
    }

    public async Task<TestProgressListResponse> GetUserTestProgress(int userId, IReadOnlyList<int> testIds)
    {
        logger.LogDebug(
            "GetUserTestProgress user={UserId} count={Count}",
            userId,
            testIds.Count);

        if (testIds.Count == 0)
            return new TestProgressListResponse();

        var normalizedIds = testIds
            .Where(id => id > 0)
            .Distinct()
            .Take(100)
            .ToList();

        var rows = await results.GetUserTestProgressAsync(userId, normalizedIds);

        return new TestProgressListResponse
        {
            Items = rows.Select(row => new TestProgressResponse
            {
                TestId = row.TestId,
                TotalQuestions = row.TotalQuestions,
                AnsweredCount = row.AnsweredCount,
                CorrectCount = row.CorrectCount,
                IncorrectCount = row.IncorrectCount,
                StartedAt = row.StartedAt,
                CompletedAt = row.CompletedAt
            }).ToList()
        };
    }
}
