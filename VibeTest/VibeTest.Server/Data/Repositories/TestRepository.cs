using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Data.Queries;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class TestRepository(AppDbContext db) : ITestRepository
{
    public Task<Test?> GetByIdAsync(int testId, CancellationToken cancellationToken = default) =>
        db.Tests.FirstOrDefaultAsync(t => t.Id == testId, cancellationToken);

    public Task<Test?> GetByIdWithStructureAsync(int testId, CancellationToken cancellationToken = default) =>
        db.Tests
            .Include(t => t.Author)
            .Include(t => t.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(t => t.Id == testId, cancellationToken);

    public async Task AddAsync(Test test, CancellationToken cancellationToken = default) =>
        await db.Tests.AddAsync(test, cancellationToken);

    public Task DeleteAsync(Test test, CancellationToken cancellationToken = default)
    {
        db.Tests.Remove(test);
        return Task.CompletedTask;
    }

    public async Task<int> GetMaxQuestionOrderAsync(int testId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """
                SELECT COALESCE(MAX("order"), -1) AS value
                FROM questions
                WHERE test_id = {0}
                """,
                testId)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public async Task<int> CountPublicTestsAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>("""SELECT COUNT(*) AS value FROM tests WHERE is_public = TRUE""")
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public Task<List<TestListItemRow>> GetPublicTestsPageAsync(
        int offset,
        int pageSize,
        string sortBy,
        string order,
        CancellationToken cancellationToken = default)
    {
        var (sortColumn, sortDirection) = ResolveTestListSort(sortBy, order);

        var sql =
            """
            SELECT
                t.id AS id,
                t.name AS name,
                t.description AS description,
                u.display_name AS author_name,
                t.questions_count AS questions_count,
                t.is_public AS is_public,
                t.difficulty AS difficulty,
                t.created_at AS created_at,
                t.updated_at AS updated_at
            FROM tests t
            INNER JOIN users u ON u.id = t.author_id
            WHERE t.is_public = TRUE
            ORDER BY 
            """ + sortColumn + " " + sortDirection + """

            LIMIT {0} OFFSET {1}
            """;

        return db.Database
            .SqlQueryRaw<TestListItemRow>(sql, pageSize, offset)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountMyTestsAsync(int authorId, string filter, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>(
                """
                SELECT COUNT(*) AS value
                FROM tests t
                WHERE t.author_id = {0}
                  AND ({1} = 'all'
                       OR ({1} = 'published' AND t.is_public = TRUE)
                       OR ({1} = 'private' AND t.is_public = FALSE))
                """,
                authorId,
                filter)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public Task<List<TestListItemRow>> GetMyTestsPageAsync(
        int authorId,
        string filter,
        int offset,
        int pageSize,
        string sortBy,
        string order,
        CancellationToken cancellationToken = default)
    {
        var (sortColumn, sortDirection) = ResolveTestListSort(sortBy, order);

        var sql =
            """
            SELECT
                t.id AS id,
                t.name AS name,
                t.description AS description,
                u.display_name AS author_name,
                t.questions_count AS questions_count,
                t.is_public AS is_public,
                t.difficulty AS difficulty,
                t.created_at AS created_at,
                t.updated_at AS updated_at
            FROM tests t
            INNER JOIN users u ON u.id = t.author_id
            WHERE t.author_id = {2}
              AND ({3} = 'all'
                   OR ({3} = 'published' AND t.is_public = TRUE)
                   OR ({3} = 'private' AND t.is_public = FALSE))
            ORDER BY 
            """ + sortColumn + " " + sortDirection + """

            LIMIT {0} OFFSET {1}
            """;

        return db.Database
            .SqlQueryRaw<TestListItemRow>(sql, pageSize, offset, authorId, filter)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    private static (string Column, string Direction) ResolveTestListSort(string sortBy, string order)
    {
        var column = sortBy == "name" ? "LOWER(t.name)" : "t.updated_at";
        var direction = order == "asc" ? "ASC" : "DESC";
        return (column, direction);
    }
}
