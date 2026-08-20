namespace VibeTest.Server.Models.Entities;

public class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsCorrect { get; set; }

    public Question Question { get; set; } = null!;
    public ICollection<Result> Results { get; set; } = [];
}
