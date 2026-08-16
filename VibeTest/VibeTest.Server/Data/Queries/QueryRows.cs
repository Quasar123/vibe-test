namespace VibeTest.Server.Data.Queries;

using VibeTest.Server.Models.Entities;

public class TestListItemRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int QuestionsCount { get; set; }
    public bool IsPublic { get; set; }
    public TestDifficulty Difficulty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TestResultSummaryRow
{
    public int TestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int IncorrectAnswers { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class TestHistoryRow
{
    public int TestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public double ScorePercent { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class UserStatsRow
{
    public int TotalCreated { get; set; }
    public int TotalPublished { get; set; }
    public int TotalPassedOwn { get; set; }
    public int TotalPassedOthers { get; set; }
    public double AverageScoreOwn { get; set; }
    public double AverageScoreOthers { get; set; }
}

public class ScalarIntRow
{
    public int Value { get; set; }
}

public class AnsweredQuestionRow
{
    public int QuestionOrder { get; set; }
    public int SelectedAnswerOrder { get; set; }
    public int CorrectAnswerOrder { get; set; }
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
}

public class TestProgressRow
{
    public int TestId { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
