namespace VibeTest.Server.Models.Entities;

public class Question
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Explanation { get; set; }

    public Test Test { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = [];
    public ICollection<Result> Results { get; set; } = [];
}
