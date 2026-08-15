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
                SELECT COALESCE(MAX("Order"), -1) AS Value
                FROM Questions
                WHERE TestId = {0}
                """,
                testId)
            .FirstAsync(cancellationToken);

        return row.Value;
    }

    public async Task<int> CountPublicTestsAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQueryRaw<ScalarIntRow>("SELECT COUNT(*) AS Value FROM Tests WHERE IsPublic = 1")
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
                t.Id AS Id,
                t.Name AS Name,
                t.Description AS Description,
                u.DisplayName AS AuthorName,
                t.QuestionsCount AS QuestionsCount,
                t.IsPublic AS IsPublic,
                t.Difficulty AS Difficulty,
                t.CreatedAt AS CreatedAt,
                t.UpdatedAt AS UpdatedAt
            FROM Tests t
            INNER JOIN Users u ON u.Id = t.AuthorId
            WHERE t.IsPublic = 1
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
                SELECT COUNT(*) AS Value
                FROM Tests t
                WHERE t.AuthorId = {0}
                  AND ({1} = 'all'
                       OR ({1} = 'published' AND t.IsPublic = 1)
                       OR ({1} = 'private' AND t.IsPublic = 0))
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
                t.Id AS Id,
                t.Name AS Name,
                t.Description AS Description,
                u.DisplayName AS AuthorName,
                t.QuestionsCount AS QuestionsCount,
                t.IsPublic AS IsPublic,
                t.Difficulty AS Difficulty,
                t.CreatedAt AS CreatedAt,
                t.UpdatedAt AS UpdatedAt
            FROM Tests t
            INNER JOIN Users u ON u.Id = t.AuthorId
            WHERE t.AuthorId = {2}
              AND ({3} = 'all'
                   OR ({3} = 'published' AND t.IsPublic = 1)
                   OR ({3} = 'private' AND t.IsPublic = 0))
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
        var column = sortBy == "name" ? "t.Name COLLATE NOCASE" : "t.UpdatedAt";
        var direction = order == "asc" ? "ASC" : "DESC";
        return (column, direction);
    }
}
