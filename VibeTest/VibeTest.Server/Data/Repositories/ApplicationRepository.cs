using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class ApplicationRepository(AppDbContext db) : IApplicationRepository
{
    private const string ListBaseCte = """
        WITH app_scores AS (
            SELECT
                ta.id AS application_id,
                ta.token,
                ta.title,
                ta.type,
                ta.test_id,
                t.name AS test_name,
                ta.created_at,
                ta.completed_at,
                ta.hide_results_from_participant,
                ta.recipient_user_id,
                t.questions_count AS total_questions,
                COALESCE(SUM(CASE WHEN sel.is_correct = TRUE THEN 1 ELSE 0 END), 0) AS correct_answers,
                CASE
                    WHEN t.questions_count > 0 THEN
                        CAST(COALESCE(SUM(CASE WHEN sel.is_correct = TRUE THEN 1 ELSE 0 END), 0) AS double precision)
                            / t.questions_count * 100.0
                    ELSE 0.0
                END AS score_percent
            FROM test_applications ta
            INNER JOIN tests t ON t.id = ta.test_id
            LEFT JOIN application_results ar ON ar.application_id = ta.id
            LEFT JOIN answers sel ON sel.id = ar.answer_id
            WHERE ta.author_id = {0}
            GROUP BY ta.id, ta.token, ta.title, ta.type, ta.test_id, t.name, ta.created_at, ta.completed_at, ta.hide_results_from_participant, ta.recipient_user_id, t.questions_count
        )
        """;

    public Task AddAsync(TestApplication application, CancellationToken cancellationToken = default)
    {
        db.TestApplications.Add(application);
        return Task.CompletedTask;
    }

    public Task<TestApplication?> GetPlayDetailByTokenAsync(Guid token, CancellationToken cancellationToken = default) =>
        db.TestApplications
            .Include(a => a.Test)
            .ThenInclude(t => t.Author)
            .Include(a => a.Test)
            .ThenInclude(t => t.Questions)
            .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(a => a.Token == token, cancellationToken);

    public Task<TestApplication?> GetByTokenForAccessAsync(Guid token, CancellationToken cancellationToken = default) =>
        db.TestApplications.FirstOrDefaultAsync(a => a.Token == token, cancellationToken);

    public Task<TestApplication?> GetByIdForAuthorAsync(int id, int authorId, CancellationToken cancellationToken = default) =>
        db.TestApplications.FirstOrDefaultAsync(a => a.Id == id && a.AuthorId == authorId, cancellationToken);

    public async Task<int> CountByAuthorAsync(int authorId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                ListBaseCte + """SELECT COUNT(*) AS value FROM app_scores""",
                authorId)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public Task<List<ApplicationListItemRow>> GetByAuthorPageAsync(
        int authorId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<ApplicationListItemRow>(
                ListBaseCte +
                """
                SELECT
                    application_id AS id,
                    token AS token,
                    title AS title,
                    type AS type,
                    test_id AS test_id,
                    test_name AS test_name,
                    created_at AS created_at,
                    completed_at AS completed_at,
                    hide_results_from_participant AS hide_results_from_participant,
                    recipient_user_id AS recipient_user_id,
                    total_questions AS total_questions,
                    correct_answers AS correct_answers,
                    score_percent AS score_percent
                FROM app_scores
                ORDER BY created_at DESC
                LIMIT {1} OFFSET {2}
                """,
                authorId,
                pageSize,
                offset)
            .ToListAsync(cancellationToken);

    public async Task<int> CountIncomingAsync(int recipientUserId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """SELECT COUNT(*) AS value FROM test_applications WHERE type = {0} AND recipient_user_id = {1}""",
                (int)ApplicationType.InternalUser,
                recipientUserId)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public Task<List<IncomingApplicationListItemRow>> GetIncomingPageAsync(
        int recipientUserId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<IncomingApplicationListItemRow>(
                """
                SELECT
                    ta.id AS id,
                    ta.token AS token,
                    ta.title AS title,
                    u.display_name AS author_name,
                    ta.test_id AS test_id,
                    t.name AS test_name,
                    ta.created_at AS created_at,
                    ta.completed_at AS completed_at,
                    ta.hide_results_from_participant AS hide_results_from_participant
                FROM test_applications ta
                INNER JOIN tests t ON t.id = ta.test_id
                INNER JOIN users u ON u.id = ta.author_id
                WHERE ta.type = {0} AND ta.recipient_user_id = {1}
                ORDER BY ta.created_at DESC
                LIMIT {2} OFFSET {3}
                """,
                (int)ApplicationType.InternalUser,
                recipientUserId,
                pageSize,
                offset)
            .ToListAsync(cancellationToken);

    public async Task<ApplicationSubmitStatus> SubmitAnswerAsync(
        int applicationId,
        int testId,
        int questionId,
        int answerId,
        DateTime answeredAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var application = await db.TestApplications.FirstOrDefaultAsync(
            a => a.Id == applicationId,
            cancellationToken);
        if (application is null || application.CompletedAt.HasValue)
            return ApplicationSubmitStatus.ApplicationCompleted;

        var existing = await db.ApplicationResults.FirstOrDefaultAsync(
            r => r.ApplicationId == applicationId && r.QuestionId == questionId,
            cancellationToken);

        if (existing is not null)
            return ApplicationSubmitStatus.QuestionAlreadyAnswered;

        db.ApplicationResults.Add(new ApplicationResult
        {
            ApplicationId = applicationId,
            QuestionId = questionId,
            AnswerId = answerId,
            AnsweredAt = answeredAt
        });

        await db.SaveChangesAsync(cancellationToken);

        var totalQuestions = await GetQuestionCountAsync(testId, cancellationToken);
        var answerCount = await GetAnswerCountAsync(applicationId, cancellationToken);
        if (answerCount >= totalQuestions && totalQuestions > 0)
        {
            application.CompletedAt = answeredAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ApplicationSubmitStatus.Success;
    }

    public Task<List<AnsweredQuestionRow>> GetAnsweredQuestionsAsync(
        int applicationId,
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
                FROM application_results ar
                INNER JOIN test_applications ta ON ta.id = ar.application_id
                INNER JOIN questions q
                    ON q.id = ar.question_id
                   AND q.test_id = ta.test_id
                INNER JOIN answers sel
                    ON sel.id = ar.answer_id
                   AND sel.question_id = ar.question_id
                INNER JOIN answers correct
                    ON correct.question_id = q.id
                   AND correct.is_correct = TRUE
                WHERE ar.application_id = {0}
                ORDER BY q."order"
                """,
                applicationId)
            .ToListAsync(cancellationToken);

    public Task<ApplicationResultSummaryRow?> GetResultSummaryAsync(
        int applicationId,
        CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<ApplicationResultSummaryRow>(
                """
                SELECT
                    t.id AS test_id,
                    t.name AS test_name,
                    t.questions_count AS total_questions,
                    COALESCE(SUM(CASE WHEN sel.is_correct = TRUE THEN 1 ELSE 0 END), 0) AS correct_answers,
                    COALESCE(SUM(CASE WHEN sel.is_correct = FALSE THEN 1 ELSE 0 END), 0) AS incorrect_answers,
                    MIN(ar.answered_at) AS started_at,
                    ta.completed_at AS completed_at
                FROM test_applications ta
                INNER JOIN tests t ON t.id = ta.test_id
                LEFT JOIN application_results ar ON ar.application_id = ta.id
                LEFT JOIN answers sel ON sel.id = ar.answer_id
                WHERE ta.id = {0}
                GROUP BY t.id, t.name, t.questions_count, ta.completed_at
                """,
                applicationId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<int> GetQuestionCountAsync(int testId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """SELECT questions_count AS value FROM tests WHERE id = {0}""",
                testId)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Value ?? 0;
    }

    private async Task<int> GetAnswerCountAsync(int applicationId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """SELECT COUNT(*) AS value FROM application_results WHERE application_id = {0}""",
                applicationId)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Value ?? 0;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
