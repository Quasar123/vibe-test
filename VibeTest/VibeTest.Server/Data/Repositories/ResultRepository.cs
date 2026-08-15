using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class ResultRepository(AppDbContext db) : IResultRepository
{
    private const string HistoryBaseCte = """
        WITH attempt_scores AS (
            SELECT
                utr.TestId,
                t.Name AS TestName,
                t.QuestionsCount AS TotalQuestions,
                utr.CorrectAnswer AS CorrectAnswers,
                CAST(utr.CorrectAnswer AS REAL) / t.QuestionsCount * 100.0 AS ScorePercent,
                (
                    SELECT MAX(r.AnsweredAt)
                    FROM Results r
                    WHERE r.UserId = utr.UserId AND r.TestId = utr.TestId
                ) AS CompletedAt
            FROM UserTestResults utr
            INNER JOIN Tests t ON t.Id = utr.TestId
            WHERE utr.UserId = {0}
              AND t.QuestionsCount > 0
              AND (utr.CorrectAnswer + utr.IncorrectAnswer) > 0
        )
        """;

    public Task<List<Result>> GetByUserAndTestAsync(int userId, int testId, CancellationToken cancellationToken = default) =>
        db.Results
            .Where(r => r.UserId == userId && r.TestId == testId)
            .ToListAsync(cancellationToken);

    public Task<Answer?> GetAnswerByOrdersAsync(
        int testId,
        int questionOrder,
        int answerOrder,
        CancellationToken cancellationToken = default) =>
        db.Answers
            .Include(a => a.Question)
            .FirstOrDefaultAsync(
                a => a.Question.TestId == testId
                    && a.Question.Order == questionOrder
                    && a.Order == answerOrder,
                cancellationToken);

    public async Task<int?> GetCorrectAnswerOrderAsync(int testId, int questionOrder, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """
                SELECT a."Order" AS Value
                FROM Answers a
                INNER JOIN Questions q ON q.Id = a.QuestionId
                WHERE q.TestId = {0}
                  AND q."Order" = {1}
                  AND a.IsCorrect = 1
                LIMIT 1
                """,
                testId,
                questionOrder)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Value;
    }

    public async Task<string?> GetQuestionExplanationAsync(
        int testId,
        int questionOrder,
        CancellationToken cancellationToken = default)
    {
        var row = await db.Questions
            .Where(q => q.TestId == testId && q.Order == questionOrder)
            .Select(q => q.Explanation)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(row) ? null : row;
    }

    public Task<TestResultSummaryRow?> GetTestResultSummaryAsync(
        int userId,
        int testId,
        CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<TestResultSummaryRow>(
                """
                SELECT
                    t.Id AS TestId,
                    t.Name AS TestName,
                    t.QuestionsCount AS TotalQuestions,
                    COALESCE(utr.CorrectAnswer, 0) AS CorrectAnswers,
                    COALESCE(utr.IncorrectAnswer, 0) AS IncorrectAnswers,
                    (
                        SELECT MIN(r.AnsweredAt)
                        FROM Results r
                        WHERE r.UserId = {0} AND r.TestId = {1}
                    ) AS StartedAt,
                    (
                        SELECT MAX(r.AnsweredAt)
                        FROM Results r
                        WHERE r.UserId = {0} AND r.TestId = {1}
                    ) AS CompletedAt
                FROM Tests t
                LEFT JOIN UserTestResults utr
                    ON utr.TestId = t.Id AND utr.UserId = {0}
                WHERE t.Id = {1}
                """,
                userId,
                testId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(Result result, CancellationToken cancellationToken = default)
    {
        db.Results.Add(result);
    }

    public Task<UserTestResult?> GetUserTestResultAsync(
        int userId,
        int testId,
        CancellationToken cancellationToken = default) =>
        db.UserTestResults
            .FirstOrDefaultAsync(utr => utr.UserId == userId && utr.TestId == testId, cancellationToken);

    public Task InsertUserTestResultAsync(UserTestResult aggregate, CancellationToken cancellationToken = default)
    {
        db.UserTestResults.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task DeleteUserTestResultAsync(int userId, int testId, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserTestResults WHERE UserId = {0} AND TestId = {1}",
            userId,
            testId);
    }

    public Task<bool> HasAnswerForQuestionAsync(
        int userId,
        int testId,
        int questionId,
        CancellationToken cancellationToken = default) =>
        db.Results.AnyAsync(
            r => r.UserId == userId && r.TestId == testId && r.QuestionId == questionId,
            cancellationToken);

    public Task<List<AnsweredQuestionRow>> GetAnsweredQuestionsAsync(
        int userId,
        int testId,
        CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<AnsweredQuestionRow>(
                """
                SELECT
                    q."Order" AS QuestionOrder,
                    sel."Order" AS SelectedAnswerOrder,
                    correct."Order" AS CorrectAnswerOrder,
                    sel.IsCorrect AS IsCorrect,
                    q.Explanation AS Explanation
                FROM Results r
                INNER JOIN Questions q
                    ON q.Id = r.QuestionId
                   AND q.TestId = r.TestId
                INNER JOIN Answers sel
                    ON sel.Id = r.AnswerId
                   AND sel.QuestionId = r.QuestionId
                INNER JOIN Answers correct
                    ON correct.QuestionId = q.Id
                   AND correct.IsCorrect = 1
                WHERE r.UserId = {0} AND r.TestId = {1}
                ORDER BY q."Order"
                """,
                userId,
                testId)
            .ToListAsync(cancellationToken);

    public async Task DeleteByUserAndTestAsync(int userId, int testId, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM Results WHERE UserId = {0} AND TestId = {1}",
            userId,
            testId);
    }

    public async Task<int> CountUserHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                HistoryBaseCte + "SELECT COUNT(*) AS Value FROM attempt_scores",
                userId)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public Task<List<TestHistoryRow>> GetUserHistoryPageAsync(
        int userId,
        string sortBy,
        string order,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sortColumn = sortBy == "score" ? "ScorePercent" : "CompletedAt";
        var sortDirection = order == "asc" ? "ASC" : "DESC";

        var sql = HistoryBaseCte +
                  """
                  SELECT
                      TestId,
                      TestName,
                      TotalQuestions,
                      CorrectAnswers,
                      ScorePercent,
                      CompletedAt
                  FROM attempt_scores
                  ORDER BY 
                  """ + sortColumn + " " + sortDirection + """

                  LIMIT {1} OFFSET {2}
                  """;

        return db.Database
            .SqlQueryRaw<TestHistoryRow>(sql, userId, pageSize, offset)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TestProgressRow>> GetUserTestProgressAsync(
        int userId,
        IReadOnlyList<int> testIds,
        CancellationToken cancellationToken = default)
    {
        if (testIds.Count == 0)
            return [];

        var distinctIds = testIds.Distinct().ToList();
        var placeholders = string.Join(", ", distinctIds.Select((_, index) => "{" + (index + 1) + "}"));
        var parameters = new object[distinctIds.Count + 1];
        parameters[0] = userId;
        for (var i = 0; i < distinctIds.Count; i++)
            parameters[i + 1] = distinctIds[i];

        var sql =
            """
            SELECT
                t.Id AS TestId,
                t.QuestionsCount AS TotalQuestions,
                COALESCE(utr.CorrectAnswer, 0) + COALESCE(utr.IncorrectAnswer, 0) AS AnsweredCount,
                COALESCE(utr.CorrectAnswer, 0) AS CorrectCount,
                COALESCE(utr.IncorrectAnswer, 0) AS IncorrectCount,
                (
                    SELECT MIN(r.AnsweredAt)
                    FROM Results r
                    WHERE r.UserId = {0} AND r.TestId = t.Id
                ) AS StartedAt,
                (
                    SELECT MAX(r.AnsweredAt)
                    FROM Results r
                    WHERE r.UserId = {0} AND r.TestId = t.Id
                ) AS CompletedAt
            FROM Tests t
            LEFT JOIN UserTestResults utr
                ON utr.TestId = t.Id AND utr.UserId = {0}
            WHERE t.Id IN (
            """ + placeholders + ")";

        return await db.Database
            .SqlQueryRaw<TestProgressRow>(sql, parameters)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
