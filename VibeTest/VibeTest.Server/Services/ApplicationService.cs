using VibeTest.Server.Data.Repositories;
using VibeTest.Server.Exceptions;
using VibeTest.Server.Helpers;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;

namespace VibeTest.Server.Services;

public class ApplicationService(
    IApplicationRepository applications,
    ITestRepository tests,
    IResultRepository results,
    IUserRepository users,
    ILogger<ApplicationService> logger) : IApplicationService
{
    public async Task<ApplicationResponse> CreateApplication(int authorId, CreateApplicationRequest request)
    {
        logger.LogDebug(
            "CreateApplication author={AuthorId} test={TestId} title={Title} type={Type} recipient={RecipientUserId}",
            authorId,
            request.TestId,
            request.Title,
            request.Type,
            request.RecipientUserId);

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Укажите название заявки");

        int? recipientUserId = null;

        switch (request.Type)
        {
            case ApplicationType.Link:
                if (request.RecipientUserId.HasValue)
                    throw new ValidationException("Для заявки по ссылке нельзя указывать получателя");
                break;

            case ApplicationType.InternalUser:
                if (!request.RecipientUserId.HasValue)
                    throw new ValidationException("Укажите пользователя для внутренней заявки");

                if (request.RecipientUserId.Value == authorId)
                    throw new ValidationException("Нельзя предложить заявку самому себе");

                _ = await users.GetDisplayNameAsync(request.RecipientUserId.Value)
                    ?? throw new NotFoundException("Пользователь не найден");

                recipientUserId = request.RecipientUserId.Value;
                break;

            default:
                throw new ValidationException("Недопустимый тип заявки");
        }

        var test = await tests.GetByIdAsync(request.TestId)
            ?? throw new NotFoundException("Тест не найден");

        if (test.AuthorId != authorId)
            throw new ForbiddenException("Доступ запрещён");

        if (test.IsPublic)
            throw new ValidationException("Заявки можно создавать только для непубличных тестов");

        var token = Guid.NewGuid();
        var application = new TestApplication
        {
            Token = token,
            AuthorId = authorId,
            TestId = test.Id,
            Title = title,
            Type = request.Type,
            RecipientUserId = recipientUserId,
            HideResultsFromParticipant = request.HideResultsFromParticipant,
            CreatedAt = DateTime.UtcNow
        };

        await applications.AddAsync(application);
        await applications.SaveChangesAsync();

        logger.LogInformation(
            "Application created: {ApplicationId} token={Token} for test {TestId}",
            application.Id,
            token,
            test.Id);

        return MapResponse(application, test.Name);
    }

    public async Task<PagedResponse<ApplicationListItem>> GetMyApplications(int authorId, int page, int pageSize)
    {
        logger.LogDebug(
            "GetMyApplications author={AuthorId} page={Page} pageSize={PageSize}",
            authorId,
            page,
            pageSize);

        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var offset = (normalizedPage - 1) * normalizedSize;

        var totalCount = await applications.CountByAuthorAsync(authorId);
        var rows = await applications.GetByAuthorPageAsync(authorId, offset, normalizedSize);

        var items = rows.Select(row => new ApplicationListItem
        {
            Id = row.Id,
            Token = row.Token,
            Title = row.Title,
            Type = row.Type,
            TestId = row.TestId,
            TestName = row.TestName,
            CreatedAt = row.CreatedAt,
            IsCompleted = row.CompletedAt.HasValue,
            CorrectAnswers = row.CorrectAnswers,
            TotalQuestions = row.TotalQuestions,
            ScorePercent = row.ScorePercent,
            CompletedAt = row.CompletedAt,
            HideResultsFromParticipant = row.HideResultsFromParticipant,
            RecipientUserId = row.RecipientUserId,
            PlayUrl = row.Type == ApplicationType.Link ? BuildPlayUrl(row.Token) : string.Empty
        }).ToList();

        return PaginationHelper.Create(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<PagedResponse<IncomingApplicationListItem>> GetIncomingApplications(
        int recipientUserId,
        int page,
        int pageSize)
    {
        logger.LogDebug(
            "GetIncomingApplications recipient={RecipientUserId} page={Page} pageSize={PageSize}",
            recipientUserId,
            page,
            pageSize);

        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        var offset = (normalizedPage - 1) * normalizedSize;

        var totalCount = await applications.CountIncomingAsync(recipientUserId);
        var rows = await applications.GetIncomingPageAsync(recipientUserId, offset, normalizedSize);

        var items = rows.Select(row => new IncomingApplicationListItem
        {
            Id = row.Id,
            Token = row.Token,
            Title = row.Title,
            AuthorName = row.AuthorName,
            TestId = row.TestId,
            TestName = row.TestName,
            CreatedAt = row.CreatedAt,
            IsCompleted = row.CompletedAt.HasValue,
            HideResultsFromParticipant = row.HideResultsFromParticipant,
            PlayUrl = BuildPlayUrl(row.Token)
        }).ToList();

        return PaginationHelper.Create(items, normalizedPage, normalizedSize, totalCount);
    }

    public async Task<ApplicationPlayResponse> GetApplicationPlayDetail(Guid token, int? currentUserId)
    {
        logger.LogDebug("GetApplicationPlayDetail token={Token}", token);

        var application = await applications.GetPlayDetailByTokenAsync(token)
            ?? throw new NotFoundException("Заявка не найдена");

        ApplicationAccessPolicy.EnsureCanPlay(application, currentUserId);

        var testDetail = MapTestDetail(application.Test);

        return new ApplicationPlayResponse
        {
            Title = application.Title,
            HideResultsFromParticipant = application.HideResultsFromParticipant,
            IsCompleted = application.CompletedAt.HasValue,
            Id = testDetail.Id,
            Name = testDetail.Name,
            Description = testDetail.Description,
            AuthorName = testDetail.AuthorName,
            Questions = testDetail.Questions
        };
    }

    public async Task<SubmitResponse> SubmitAnswer(Guid token, SubmitAnswerRequest request, int? currentUserId)
    {
        logger.LogDebug(
            "SubmitAnswer application token={Token} question={QuestionOrder} answer={AnswerOrder}",
            token,
            request.QuestionOrder,
            request.SelectedAnswerOrder);

        var application = await applications.GetByTokenForAccessAsync(token)
            ?? throw new NotFoundException("Заявка не найдена");

        ApplicationAccessPolicy.EnsureCanPlay(application, currentUserId);

        if (application.CompletedAt.HasValue)
            throw new ValidationException("Тест по этой заявке уже пройден");

        var testId = application.TestId;

        var selected = await results.GetAnswerByOrdersAsync(testId, request.QuestionOrder, request.SelectedAnswerOrder)
            ?? throw new ValidationException("Неверный вопрос или ответ");

        var correctOrder = await results.GetCorrectAnswerOrderAsync(testId, request.QuestionOrder)
            ?? throw new ValidationException("Для вопроса не задан правильный ответ");

        var now = DateTime.UtcNow;
        var status = await applications.SubmitAnswerAsync(
            application.Id,
            testId,
            selected.QuestionId,
            selected.Id,
            now);
        switch (status)
        {
            case ApplicationSubmitStatus.QuestionAlreadyAnswered:
                throw new ValidationException("На этот вопрос уже дан ответ");
            case ApplicationSubmitStatus.ApplicationCompleted:
                throw new ValidationException("Тест по этой заявке уже пройден");
        }

        if (application.HideResultsFromParticipant)
            return new SubmitResponse { CorrectAnswerOrder = -1, Explanation = null };

        var explanation = await results.GetQuestionExplanationAsync(testId, request.QuestionOrder);

        return new SubmitResponse { CorrectAnswerOrder = correctOrder, Explanation = explanation };
    }

    public async Task<TestResultResponse> GetApplicationResult(Guid token, int? currentUserId)
    {
        logger.LogDebug("GetApplicationResult token={Token}", token);

        var application = await applications.GetByTokenForAccessAsync(token)
            ?? throw new NotFoundException("Заявка не найдена");

        ApplicationAccessPolicy.EnsureCanPlay(application, currentUserId);

        if (application.HideResultsFromParticipant)
            throw new ForbiddenException("Результат недоступен");

        return await BuildResultResponse(application.Id);
    }

    public async Task<TestResultResponse> GetApplicationResultById(int applicationId, int authorId)
    {
        logger.LogDebug("GetApplicationResultById id={ApplicationId} author={AuthorId}", applicationId, authorId);

        _ = await applications.GetByIdForAuthorAsync(applicationId, authorId)
            ?? throw new NotFoundException("Заявка не найдена");

        return await BuildResultResponse(applicationId);
    }

    private async Task<TestResultResponse> BuildResultResponse(int applicationId)
    {
        var summary = await applications.GetResultSummaryAsync(applicationId)
            ?? throw new NotFoundException("Заявка не найдена");

        return new TestResultResponse
        {
            TestId = summary.TestId,
            TestName = summary.TestName,
            TotalQuestions = summary.TotalQuestions,
            CorrectAnswers = summary.CorrectAnswers,
            IncorrectAnswers = summary.IncorrectAnswers,
            StartedAt = summary.StartedAt,
            CompletedAt = summary.CompletedAt
        };
    }

    private static ApplicationResponse MapResponse(TestApplication application, string testName) => new()
    {
        Id = application.Id,
        Token = application.Token,
        Title = application.Title,
        Type = application.Type,
        TestId = application.TestId,
        TestName = testName,
        CreatedAt = application.CreatedAt,
        HideResultsFromParticipant = application.HideResultsFromParticipant,
        PlayUrl = application.Type == ApplicationType.Link ? BuildPlayUrl(application.Token) : string.Empty
    };

    private static TestDetailResponse MapTestDetail(Test test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Description = test.Description,
        AuthorName = test.Author.DisplayName,
        Questions = QuestionMapper.ToDetailQuestions(test.Questions)
    };

    private static string BuildPlayUrl(Guid token) => $"/application/{token}";

    public async Task<AnsweredQuestionsResponse> GetApplicationAnsweredQuestions(Guid token, int? currentUserId)
    {
        logger.LogDebug("GetApplicationAnsweredQuestions token={Token}", token);

        var application = await applications.GetByTokenForAccessAsync(token)
            ?? throw new NotFoundException("Заявка не найдена");

        ApplicationAccessPolicy.EnsureCanPlay(application, currentUserId);

        var rows = await applications.GetAnsweredQuestionsAsync(application.Id);
        var hideResults = application.HideResultsFromParticipant;

        return new AnsweredQuestionsResponse
        {
            Answers = rows.Select(row => hideResults
                ? new AnsweredQuestionResponse
                {
                    QuestionOrder = row.QuestionOrder,
                    SelectedAnswerOrder = row.SelectedAnswerOrder,
                    CorrectAnswerOrder = row.SelectedAnswerOrder,
                    IsCorrect = true,
                    Explanation = null
                }
                : new AnsweredQuestionResponse
                {
                    QuestionOrder = row.QuestionOrder,
                    SelectedAnswerOrder = row.SelectedAnswerOrder,
                    CorrectAnswerOrder = row.CorrectAnswerOrder,
                    IsCorrect = row.IsCorrect,
                    Explanation = string.IsNullOrWhiteSpace(row.Explanation) ? null : row.Explanation
                }).ToList()
        };
    }
}
