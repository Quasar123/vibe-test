using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Data.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Id == userId, cancellationToken);

    public Task<string?> GetDisplayNameAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<UserSearchResult>> SearchAsync(
        string query,
        int excludeUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var lower = query.ToLower();

        return db.Users
            .Where(u => u.Id != excludeUserId
                && (u.DisplayName.ToLower().Contains(lower) || u.Email.ToLower().Contains(lower)))
            .OrderBy(u => u.DisplayName)
            .Take(limit)
            .Select(u => new UserSearchResult { Id = u.Id, DisplayName = u.DisplayName })
            .ToListAsync(cancellationToken);
    }

    public Task<UserStatsRow> GetStatsAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Database
            .SqlQueryRaw<UserStatsRow>(
                """
                SELECT
                    (SELECT COUNT(*) FROM tests WHERE author_id = {0}) AS total_created,
                    (SELECT COUNT(*) FROM tests WHERE author_id = {0} AND is_public = TRUE) AS total_published,
                    (
                        SELECT COUNT(*)
                        FROM user_test_results utr
                        INNER JOIN tests t ON t.id = utr.test_id AND t.author_id = {0}
                        WHERE utr.user_id = {0}
                          AND t.questions_count > 0
                          AND utr.correct_answer + utr.incorrect_answer = t.questions_count
                    ) AS total_passed_own,
                    (
                        SELECT COUNT(*)
                        FROM user_test_results utr
                        INNER JOIN tests t ON t.id = utr.test_id AND t.author_id <> {0}
                        WHERE utr.user_id = {0}
                          AND t.questions_count > 0
                          AND utr.correct_answer + utr.incorrect_answer = t.questions_count
                    ) AS total_passed_others,
                    COALESCE((
                        SELECT AVG(CAST(utr.correct_answer AS double precision) / t.questions_count * 100.0)
                        FROM user_test_results utr
                        INNER JOIN tests t ON t.id = utr.test_id AND t.author_id = {0}
                        WHERE utr.user_id = {0}
                          AND t.questions_count > 0
                          AND utr.correct_answer + utr.incorrect_answer = t.questions_count
                    ), 0.0) AS average_score_own,
                    COALESCE((
                        SELECT AVG(CAST(utr.correct_answer AS double precision) / t.questions_count * 100.0)
                        FROM user_test_results utr
                        INNER JOIN tests t ON t.id = utr.test_id AND t.author_id <> {0}
                        WHERE utr.user_id = {0}
                          AND t.questions_count > 0
                          AND utr.correct_answer + utr.incorrect_answer = t.questions_count
                    ), 0.0) AS average_score_others
                """,
                userId)
            .FirstAsync(cancellationToken);
}
