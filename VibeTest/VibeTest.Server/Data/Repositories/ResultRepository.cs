using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class ResultRepository(AppDbContext db) : IResultRepository
{
    private const string HistoryBaseCte = """
        WITH attempt_scores AS (
            SELECT
                utr.test_id,
                t.name AS test_name,
                t.questions_count AS total_questions,
                utr.correct_answer AS correct_answers,
                CAST(utr.correct_answer AS double precision) / t.questions_count * 100.0 AS score_percent,
                (
                    SELECT MAX(r.answered_at)
                    FROM results r
                    WHERE r.user_id = utr.user_id AND r.test_id = utr.test_id
                ) AS completed_at
            FROM user_test_results utr
            INNER JOIN tests t ON t.id = utr.test_id
            WHERE utr.user_id = {0}
              AND t.questions_count > 0
              AND (utr.correct_answer + utr.incorrect_answer) > 0
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
                SELECT a."order" AS value
                FROM answers a
                INNER JOIN questions q ON q.id = a.question_id
                WHERE q.test_id = {0}
                  AND q."order" = {1}
                  AND a.is_correct = TRUE
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
                    t.id AS test_id,
                    t.name AS test_name,
                    t.questions_count AS total_questions,
                    COALESCE(utr.correct_answer, 0) AS correct_answers,
                    COALESCE(utr.incorrect_answer, 0) AS incorrect_answers,
                    (
                        SELECT MIN(r.answered_at)
                        FROM results r
                        WHERE r.user_id = {0} AND r.test_id = {1}
                    ) AS started_at,
                    (
                        SELECT MAX(r.answered_at)
                        FROM results r
                        WHERE r.user_id = {0} AND r.test_id = {1}
                    ) AS completed_at
                FROM tests t
                LEFT JOIN user_test_results utr
                    ON utr.test_id = t.id AND utr.user_id = {0}
                WHERE t.id = {1}
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
            """DELETE FROM user_test_results WHERE user_id = {0} AND test_id = {1}""",
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
                    q."order" AS question_order,
                    sel."order" AS selected_answer_order,
                    correct."order" AS correct_answer_order,
                    sel.is_correct AS is_correct,
                    q.explanation AS explanation
                FROM results r
                INNER JOIN questions q
                    ON q.id = r.question_id
                   AND q.test_id = r.test_id
                INNER JOIN answers sel
                    ON sel.id = r.answer_id
                   AND sel.question_id = r.question_id
                INNER JOIN answers correct
                    ON correct.question_id = q.id
                   AND correct.is_correct = TRUE
                WHERE r.user_id = {0} AND r.test_id = {1}
                ORDER BY q."order"
                """,
                userId,
                testId)
            .ToListAsync(cancellationToken);

    public async Task DeleteByUserAndTestAsync(int userId, int testId, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """DELETE FROM results WHERE user_id = {0} AND test_id = {1}""",
            userId,
            testId);
    }

    public async Task<int> CountUserHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                HistoryBaseCte + """SELECT COUNT(*) AS value FROM attempt_scores""",
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
        var sortColumn = sortBy == "score" ? "score_percent" : "completed_at";
        var sortDirection = order == "asc" ? "ASC" : "DESC";

        var sql = HistoryBaseCte +
                  """
                  SELECT
                      test_id AS test_id,
                      test_name AS test_name,
                      total_questions AS total_questions,
                      correct_answers AS correct_answers,
                      score_percent AS score_percent,
                      completed_at AS completed_at
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
                t.id AS test_id,
                t.questions_count AS total_questions,
                COALESCE(utr.correct_answer, 0) + COALESCE(utr.incorrect_answer, 0) AS answered_count,
                COALESCE(utr.correct_answer, 0) AS correct_count,
                COALESCE(utr.incorrect_answer, 0) AS incorrect_count,
                (
                    SELECT MIN(r.answered_at)
                    FROM results r
                    WHERE r.user_id = {0} AND r.test_id = t.id
                ) AS started_at,
                (
                    SELECT MAX(r.answered_at)
                    FROM results r
                    WHERE r.user_id = {0} AND r.test_id = t.id
                ) AS completed_at
            FROM tests t
            LEFT JOIN user_test_results utr
                ON utr.test_id = t.id AND utr.user_id = {0}
            WHERE t.id IN (
            """ + placeholders + ")";

        return await db.Database
            .SqlQueryRaw<TestProgressRow>(sql, parameters)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
