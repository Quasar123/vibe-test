using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Helpers;

public static class QuestionMapper
{
    public static List<QuestionDetailDto> ToDetailQuestions(IEnumerable<Question> questions) =>
        OrderQuestions(questions)
            .Select(q => new QuestionDetailDto
            {
                Order = q.Order,
                Text = q.Text,
                Answers = OrderAnswers(q.Answers)
                    .Select(a => new AnswerDetailDto
                    {
                        Order = a.Order,
                        Text = a.Text
                    })
                    .ToList()
            })
            .ToList();

    public static List<QuestionFullDto> ToFullQuestions(IEnumerable<Question> questions) =>
        OrderQuestions(questions)
            .Select(q =>
            {
                var orderedAnswers = OrderAnswers(q.Answers).ToList();
                var correctIndex = orderedAnswers.FindIndex(a => a.IsCorrect);
                return new QuestionFullDto
                {
                    Order = q.Order,
                    Text = q.Text,
                    Answers = orderedAnswers.Select(a => a.Text).ToList(),
                    Correct = correctIndex < 0 ? 0 : correctIndex,
                    Explanation = q.Explanation
                };
            })
            .ToList();

    private static IEnumerable<Question> OrderQuestions(IEnumerable<Question> questions) =>
        questions.OrderBy(q => q.Order);

    private static IEnumerable<Answer> OrderAnswers(IEnumerable<Answer> answers) =>
        answers.OrderBy(a => a.Order);
}
