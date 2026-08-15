namespace VibeTest.Server.Models.Entities;

public class Test
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public TestDifficulty Difficulty { get; set; } = TestDifficulty.Easy;
    public int QuestionsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Author { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<Result> Results { get; set; } = [];
    public ICollection<UserTestResult> UserTestResults { get; set; } = [];
}
