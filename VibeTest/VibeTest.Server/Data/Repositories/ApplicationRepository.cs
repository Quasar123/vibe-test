using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class ApplicationRepository(AppDbContext db) : IApplicationRepository
{
    private const string ListBaseCte = """
        WITH app_scores AS (
            SELECT
                ta.Id AS ApplicationId,
                ta.Token,
                ta.Title,
                ta.Type,
                ta.TestId,
                t.Name AS TestName,
                ta.CreatedAt,
                ta.CompletedAt,
                ta.HideResultsFromParticipant,
                ta.RecipientUserId,
                t.QuestionsCount AS TotalQuestions,
                COALESCE(SUM(CASE WHEN sel.IsCorrect = 1 THEN 1 ELSE 0 END), 0) AS CorrectAnswers,
                CASE
                    WHEN t.QuestionsCount > 0 THEN
                        CAST(COALESCE(SUM(CASE WHEN sel.IsCorrect = 1 THEN 1 ELSE 0 END), 0) AS REAL)
                            / t.QuestionsCount * 100.0
                    ELSE 0.0
                END AS ScorePercent
            FROM TestApplications ta
            INNER JOIN Tests t ON t.Id = ta.TestId
            LEFT JOIN ApplicationResults ar ON ar.ApplicationId = ta.Id
            LEFT JOIN Answers sel ON sel.Id = ar.AnswerId
            WHERE ta.AuthorId = {0}
            GROUP BY ta.Id, ta.Token, ta.Title, ta.Type, ta.TestId, t.Name, ta.CreatedAt, ta.CompletedAt, ta.HideResultsFromParticipant, ta.RecipientUserId, t.QuestionsCount
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
                ListBaseCte + "SELECT COUNT(*) AS Value FROM app_scores",
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
                    ApplicationId AS Id,
                    Token,
                    Title,
                    Type,
                    TestId,
                    TestName,
                    CreatedAt,
                    CompletedAt,
                    HideResultsFromParticipant,
                    RecipientUserId,
                    TotalQuestions,
                    CorrectAnswers,
                    ScorePercent
                FROM app_scores
                ORDER BY CreatedAt DESC
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
                "SELECT COUNT(*) AS Value FROM TestApplications WHERE Type = {0} AND RecipientUserId = {1}",
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
                    ta.Id AS Id,
                    ta.Token AS Token,
                    ta.Title AS Title,
                    u.DisplayName AS AuthorName,
                    ta.TestId AS TestId,
                    t.Name AS TestName,
                    ta.CreatedAt AS CreatedAt,
                    ta.CompletedAt AS CompletedAt,
                    ta.HideResultsFromParticipant AS HideResultsFromParticipant
                FROM TestApplications ta
                INNER JOIN Tests t ON t.Id = ta.TestId
                INNER JOIN Users u ON u.Id = ta.AuthorId
                WHERE ta.Type = {0} AND ta.RecipientUserId = {1}
                ORDER BY ta.CreatedAt DESC
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
                    q."Order" AS QuestionOrder,
                    sel."Order" AS SelectedAnswerOrder,
                    correct."Order" AS CorrectAnswerOrder,
                    sel.IsCorrect AS IsCorrect,
                    q.Explanation AS Explanation
                FROM ApplicationResults ar
                INNER JOIN TestApplications ta ON ta.Id = ar.ApplicationId
                INNER JOIN Questions q
                    ON q.Id = ar.QuestionId
                   AND q.TestId = ta.TestId
                INNER JOIN Answers sel
                    ON sel.Id = ar.AnswerId
                   AND sel.QuestionId = ar.QuestionId
                INNER JOIN Answers correct
                    ON correct.QuestionId = q.Id
                   AND correct.IsCorrect = 1
                WHERE ar.ApplicationId = {0}
                ORDER BY q."Order"
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
                    t.Id AS TestId,
                    t.Name AS TestName,
                    t.QuestionsCount AS TotalQuestions,
                    COALESCE(SUM(CASE WHEN sel.IsCorrect = 1 THEN 1 ELSE 0 END), 0) AS CorrectAnswers,
                    COALESCE(SUM(CASE WHEN sel.IsCorrect = 0 THEN 1 ELSE 0 END), 0) AS IncorrectAnswers,
                    MIN(ar.AnsweredAt) AS StartedAt,
                    ta.CompletedAt AS CompletedAt
                FROM TestApplications ta
                INNER JOIN Tests t ON t.Id = ta.TestId
                LEFT JOIN ApplicationResults ar ON ar.ApplicationId = ta.Id
                LEFT JOIN Answers sel ON sel.Id = ar.AnswerId
                WHERE ta.Id = {0}
                GROUP BY t.Id, t.Name, t.QuestionsCount, ta.CompletedAt
                """,
                applicationId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<int> GetQuestionCountAsync(int testId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                "SELECT QuestionsCount AS Value FROM Tests WHERE Id = {0}",
                testId)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Value ?? 0;
    }

    private async Task<int> GetAnswerCountAsync(int applicationId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                "SELECT COUNT(*) AS Value FROM ApplicationResults WHERE ApplicationId = {0}",
                applicationId)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Value ?? 0;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
