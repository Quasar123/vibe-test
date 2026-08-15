# VibeTest — Техническая спецификация

> **Навигация:** практические гайды и оглавление — в [docs/README.md](README.md). Краткий обзор API: [api-reference.md](api-reference.md).  
> **Примечание:** раздел [§9. Этапы разработки](#9-этапы-разработки) — исторический план; описанные сервисы, API и фронтенд уже реализованы.

## 1. Описание проекта

**VibeTest** — конструктор тестов, работающий в браузере. Позволяет создавать, импортировать, проходить тесты и получать результаты. Регистрация добавляет облачное сохранение, публикацию и историю прохождений.

**Пользовательские сценарии**

Гость

· Просмотр списка публичных тестов с пагинацией
· Создание теста в редакторе → сохранение в localStorage
· Импорт теста из JSON (файл или буфер обмена)
· Прохождение локальных и импортированных тестов
· Просмотр результатов сразу после прохождения
· Экспорт теста в JSON (файл или буфер обмена)

Пользователь

Всё из гостевого доступа, плюс:

· Прохождение публичных тестов с сохранением результатов на сервере
· Сохранение тестов на сервер (изначально приватные)
· Публикация и скрытие тестов
· Дополнение тестов новыми вопросами (без удаления старых)
· Копия своего теста через выгрузку в localStorage/JSON и загрузку обратно как нового теста
· Изменение названия и описания теста
· Просмотр истории прохождений с баллами и датами
· Статистика: создано/опубликовано/пройдено/средний балл
· Дорешивание теста при добавлении новых вопросов
· Пересдача тестов (удаление старого результата)


**Стек технологий**:
- **Бэкенд**: ASP.NET Core 10 (`VibeTest.Server`), Entity Framework Core, SQLite
- **Фронтенд**: React + TypeScript + Vite (`vibetest.client`)
- **Режимы фронтенда**: `guest` (автономный, без API) и `full` (с бэкендом)
- **Деплой гостевого SPA**: GitHub Pages (`npm run build:guest`)
- **Аутентификация**: JWT (access + refresh токены) — только в режиме `full`, Этап 2
- **Хранение на клиенте**: localStorage для гостевых тестов и прогресса

**Пользовательские роли**:
- **Гость** — создаёт локальные тесты в localStorage, проходит их, импортирует/экспортирует JSON
- **Пользователь** — всё из гостевого доступа + сохранение тестов на сервер, прохождение публичных тестов с сохранением результатов, статистика

---

## 2. Структура проекта

Проект основан на **шаблоне Visual Studio «ASP.NET Core with React»**. Имена и расположение `VibeTest.Server` и `vibetest.client` не меняются — доменный слой добавляется внутрь серверного проекта.

```
VibeTest/
├── VibeTest.slnx
├── VibeTest.Server/                       # ASP.NET Core + доменный слой
│   ├── Controllers/                       # Этап 2
│   │   ├── AuthController.cs
│   │   └── TestsController.cs
│   │
│   ├── Services/
│   │   ├── ITestService.cs
│   │   ├── TestService.cs
│   │   ├── IResultService.cs
│   │   ├── ResultService.cs
│   │   ├── IUserService.cs
│   │   ├── UserService.cs
│   │   └── (IAuthService, AuthService)      # Этап 2
│   │
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── Repositories/
│   │   │   ├── ITestRepository.cs
│   │   │   ├── TestRepository.cs
│   │   │   ├── IResultRepository.cs
│   │   │   └── ResultRepository.cs
│   │   └── Migrations/                    # dotnet ef migrations add
│   │
│   ├── Models/
│   │   ├── Entities/
│   │   ├── Requests/
│   │   └── Responses/
│   │
│   ├── Helpers/
│   │   ├── QuestionMapper.cs              # Questions/Answers → DTO
│   │   └── PaginationHelper.cs
│   │
│   ├── Exceptions/
│   │   └── DomainExceptions.cs            # NotFound, Forbidden, Validation
│   │
│   ├── ServiceCollectionExtensions.cs     # AddVibeTestServices()
│   ├── Program.cs                         # API + SPA static (режим full)
│   └── VibeTest.Server.csproj
│
├── vibetest.client/                       # React + Vite
│   ├── .env.guest                         # VITE_APP_MODE=guest, VITE_BASE_PATH
│   ├── .env.full                          # VITE_APP_MODE=full, proxy к API
│   ├── src/
│   │   ├── config/
│   │   │   └── env.ts                     # appMode, basePath, isGuest
│   │   ├── guest/                         # Автономный режим (GitHub Pages)
│   │   │   ├── GuestApp.tsx               # Роутинг без API
│   │   │   ├── pages/
│   │   │   └── components/
│   │   ├── full/                          # Режим с бэкендом
│   │   │   ├── FullApp.tsx
│   │   │   ├── api/
│   │   │   ├── context/
│   │   │   ├── pages/
│   │   │   └── components/
│   │   ├── types/                         # Общие типы (тест, вопрос, JSON)
│   │   ├── utils/                         # storage, export, import
│   │   ├── App.tsx                        # guest | full по VITE_APP_MODE
│   │   └── main.tsx
│   ├── package.json                       # dev / dev:full / build:guest
│   └── vite.config.ts
│
├── .github/workflows/
│   └── deploy-guest.yml                   # GitHub Pages: build:guest
│
└── VibeTest.Tests/
    ├── Integration/
    │   ├── SqliteTestDb.cs                # SQLite in-memory fixture
    │   ├── TestServiceTests.cs
    │   ├── ResultServiceTests.cs
    │   └── UserServiceTests.cs
    └── VibeTest.Tests.csproj
```

**Пространства имён**: `VibeTest.Server.{Entities|Data|Services|Models|Helpers|Exceptions}`.

### Режимы фронтенда

| Режим | `VITE_APP_MODE` | Запуск | API | Деплой |
|-------|-----------------|--------|-----|--------|
| **guest** | `guest` | `npm run dev` | Нет | GitHub Pages (`build:guest`) |
| **full** | `full` | `npm run dev:full` + `VibeTest.Server` | Да | `VibeTest.Server` (статика из `dist`) |

**Guest (standalone)** — подмножество гостевых сценариев без бэкенда:

- Редактор → localStorage
- Импорт / экспорт JSON
- Локальные тесты, прохождение, результаты
- **Нет**: публичные тесты с сервера, вход/регистрация, облачное сохранение

**Full** — все сценарии: guest-функции + API (публичные тесты, аккаунт, статистика).

Переменные окружения (Vite):

| Переменная | guest | full |
|------------|-------|------|
| `VITE_APP_MODE` | `guest` | `full` |
| `VITE_BASE_PATH` | `/` или `/repo-name/` для GH Pages | `/` |
| `VITE_API_URL` | — | `/api` (proxy в dev) |

---

## 3. Структура базы данных

### SQL-запросы на создание таблиц

```sql
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE Tests (
    Id INTEGER PRIMARY KEY,
    AuthorId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    IsPublic INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (AuthorId) REFERENCES Users(Id) ON DELETE RESTRICT
);

CREATE TABLE Questions (
    Id INTEGER PRIMARY KEY,
    TestId INTEGER NOT NULL,
    Text TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    Explanation TEXT,
    FOREIGN KEY (TestId) REFERENCES Tests(Id) ON DELETE CASCADE,
    UNIQUE(TestId, "Order")
);

CREATE TABLE Answers (
    Id INTEGER PRIMARY KEY,
    QuestionId INTEGER NOT NULL,
    Text TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    IsCorrect INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    UNIQUE(QuestionId, "Order")
);

CREATE TABLE Results (
    Id INTEGER PRIMARY KEY,
    UserId INTEGER NOT NULL,
    TestId INTEGER NOT NULL,
    QuestionId INTEGER NOT NULL,
    AnswerId INTEGER NOT NULL,
    AnsweredAt TEXT NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (TestId) REFERENCES Tests(Id) ON DELETE CASCADE,
    FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE RESTRICT,
    FOREIGN KEY (AnswerId) REFERENCES Answers(Id) ON DELETE RESTRICT,
    UNIQUE(UserId, TestId, QuestionId)
);
```

### Примечания к схеме

- **Структура теста** — иерархия `Tests → Questions → Answers`; каждый вопрос и ответ принадлежит конкретному тесту.
- **Explanation** хранится в `Questions.Explanation` (per-test, per-question).
- **Result**: `UNIQUE(UserId, TestId, QuestionId)` — повторный submit запрещён (insert-only).
- Порядок вопросов/ответов — **0-based** (`Questions.Order`, `Answers.Order`).

---

## 4. Модели данных (C#)

Сущности — в `VibeTest.Server/Models/Entities/`. Конфигурация FK и UNIQUE — в `AppDbContext.OnModelCreating`.

```csharp
// User.cs
public class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Test.cs
public class Test
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User Author { get; set; }
    public List<Question> Questions { get; set; }
}

// Question.cs
public class Question
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public string Text { get; set; }
    public int Order { get; set; }
    public string? Explanation { get; set; }
    public List<Answer> Answers { get; set; }
}

// Answer.cs
public class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; }
    public int Order { get; set; }
    public bool IsCorrect { get; set; }
}

// Result.cs
public class Result
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TestId { get; set; }
    public int QuestionId { get; set; }
    public int AnswerId { get; set; }
    public DateTime AnsweredAt { get; set; }
    
    public User User { get; set; }
    public Test Test { get; set; }
    public Question Question { get; set; }
    public Answer Answer { get; set; }
}
```

---

## 5. Интерфейсы сервисов

```csharp
public interface ITestService
{
    // Публичные
    Task<PagedResponse<TestListItem>> GetPublicTests(int page, int pageSize);
    Task<TestDetailResponse> GetTestDetail(int testId);
    
    // Авторизованные — создание и редактирование
    Task<TestResponse> CreateTest(int authorId, CreateTestRequest request);
    Task<TestResponse> AppendQuestions(int testId, int authorId, AddQuestionsRequest request);
    Task<TestResponse> UpdateTestInfo(int testId, int authorId, UpdateTestInfoRequest request);
    Task PublishTest(int testId, int authorId);
    Task UnpublishTest(int testId, int authorId);
    Task DeleteTest(int testId, int authorId);
    
    // Авторизованные — свои тесты
    Task<PagedResponse<TestListItem>> GetMyTests(int authorId, int page, int pageSize, string filter);
    Task<TestFullResponse> GetTestFull(int testId, int authorId);
}

public interface IResultService
{
    Task<SubmitResponse> SubmitAnswer(int userId, int testId, SubmitAnswerRequest request);
    Task<TestResultResponse> GetResult(int userId, int testId);
    Task DeleteResult(int userId, int testId);
    Task<PagedResponse<TestHistoryItem>> GetUserResults(int userId, int page, int pageSize, string sortBy, string order);
}

public interface IUserService
{
    Task<UserStatsResponse> GetStats(int userId);
}
```

### Репозитории

| Интерфейс | Ответственность |
|-----------|-----------------|
| `ITestRepository` | CRUD теста, загрузка с Questions + Answers |
| `IResultRepository` | Insert-only Results, удаление по user+test, выборки |

Сервисы не содержат сложный LINQ — запросы в репозиториях.

### Доменные исключения

| Исключение | Когда | HTTP (Этап 2) |
|------------|-------|---------------|
| `NotFoundException` | Тест/вопрос не найден | 404 |
| `ForbiddenException` | Не автор теста | 403 |
| `ValidationException` | Невалидный request | 400 |

### Вспомогательные классы

- **`QuestionMapper`** — `IEnumerable<Question>` → `QuestionDetailDto` / `QuestionFullDto`
- **`PaginationHelper`** — расчёт `totalPages`, `hasNextPage`, `hasPreviousPage`

---

## 6. API-эндпоинты

### Публичные

**GET /api/tests?page=1&pageSize=10**

Возвращает список публичных тестов с пагинацией.

```json
{
  "items": [{
    "id": "int",
    "name": "SQL Basics",
    "description": "Test your SQL knowledge",
    "authorName": "Alice",
    "questionsCount": 10,
    "createdAt": "2024-01-15T10:30:00Z"
  }],
  "page": 1,
  "pageSize": 10,
  "totalCount": 42,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**GET /api/tests/{id}**

Детальная информация о тесте (без правильных ответов).

```json
{
  "id": "int",
  "name": "SQL Basics",
  "description": "Test your SQL knowledge",
  "authorName": "Alice",
  "questions": [{
    "order": 0,
    "text": "What does SQL stand for?",
    "answers": [
      { "order": 0, "text": "Structured Query Language" },
      { "order": 1, "text": "Simple Query Logic" }
    ]
  }]
}
```

### Аутентификация

**POST /api/auth/register**

```json
// Request
{
  "email": "user@example.com",
  "password": "password123",
  "displayName": "Alice"
}

// Response
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2024-01-15T10:45:00Z",
  "user": {
    "id": "int",
    "displayName": "Alice",
    "email": "user@example.com"
  }
}
```

**POST /api/auth/login** — аналогично register

**POST /api/auth/refresh**

```json
// Request
{ "refreshToken": "..." }

// Response
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2024-01-15T10:45:00Z"
}
```

**GET /api/auth/me** — возвращает текущего пользователя

### Авторизованные (require JWT)

**POST /api/tests** — создать тест

```json
// Request
{
  "name": "SQL Fundamentals",
  "description": "Basic SQL concepts",
  "questions": [{
    "text": "What does SQL stand for?",
    "answers": [
      "Structured Query Language",
      "Simple Query Logic"
    ],
    "correct": 0,
    "explanation": "SQL means Structured Query Language."
  }]
}

// Response
{
  "id": "int",
  "name": "SQL Fundamentals",
  "description": "Basic SQL concepts",
  "isPublic": false,
  "questionsCount": 1,
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**PATCH /api/tests/{id}** — дополнить тест вопросами (request как у POST)

**PATCH /api/tests/{id}/info** — изменить название/описание

```json
{ "name": "New name", "description": "New description" }
```

**PUT /api/tests/{id}/publish** — опубликовать

**PUT /api/tests/{id}/unpublish** — скрыть

**DELETE /api/tests/{id}** — удалить

**GET /api/tests/my?page=1&pageSize=10&filter=all** — мои тесты (filter: all | published | private)

**GET /api/tests/my/stats** — статистика

```json
{
  "totalCreated": 5,
  "totalPublished": 3,
  "totalPassedOwn": 2,
  "totalPassedOthers": 10,
  "averageScoreOwn": 85.0,
  "averageScoreOthers": 76.2
}
```

**GET /api/tests/{id}/full** — полная информация о своём тесте (с правильными ответами)

### Прохождение тестов

**POST /api/tests/{id}/submit** — ответить на вопрос

```json
// Request
{ "questionOrder": 0, "selectedAnswerOrder": 2 }

// Response
{ "correctAnswerOrder": 0, "explanation": "optional text when question has explanation" }
```

**GET /api/tests/{id}/result** — получить результат

```json
{
  "testId": 123,
  "testName": "SQL Basics",
  "totalQuestions": 10,
  "correctAnswers": 7,
  "incorrectAnswers": 3,
  "startedAt": "2024-01-15T10:35:00Z",
  "completedAt": "2024-01-15T10:35:00Z"
}
```

**DELETE /api/tests/{id}/result** — удалить результат (пересдача)

**GET /api/results?page=1&pageSize=20&sortBy=date&order=desc** — история прохождений

---

## 7. Бизнес-логика

### Иммутабельность

- Вопросы и ответы неизменяемы после создания
- Тест можно только дополнять новыми вопросами
- Нельзя изменить текст вопроса, ответа или правильность ответа
- Нельзя удалить вопрос из теста

### Дедупликация

При создании вопроса или ответа:
1. Поиск по точному совпадению `Text`
2. Если найден — используется существующий Id
3. Если нет — создаётся новый

### Дополнение теста

- Только автор может дополнять тест
- Новые вопросы получают следующие порядковые номера (`MAX(QuestionOrder) + 1`)
- Существующие вопросы не затрагиваются
- Результаты пользователей сохраняются — пользователь может дорешать новые вопросы

### Обмен cloud ↔ local

Копирование облачного теста выполняется на клиенте без отдельного API:

1. **Облако → локально** — `GET /api/tests/{id}/full` (только автор) → сохранение в localStorage или скачивание JSON (`testTransfer.ts`).
2. **Локально → облако** — чтение из localStorage → `POST /api/tests` (создаётся новый тест, не связанный с исходным id).

При коллизии имён в localStorage оба теста остаются в списке (новый UUID).

### Прохождение теста

1. Клиент получает тест через `GET /api/tests/{id}`
2. Пользователь выбирает ответ на вопрос
3. `POST /api/tests/{id}/submit` — сервер находит ответ по `Questions.Order` / `Answers.Order`, сохраняет Result
4. Ответ: `correctAnswerOrder`; опционально `explanation` (из `Questions.Explanation`, если задано; не отдаётся в `GET /tests/{id}`)
5. Клиент сравнивает, показывает результат
6. Можно отвечать в любом порядке, переотвечивать
7. `GET /api/tests/{id}/result` — полная статистика

### Статистика пользователя

- **Пройденный тест** — пользователь ответил на все текущие вопросы теста (`UserTestResults.CorrectAnswer + IncorrectAnswer == Tests.QuestionsCount`).
- **Балл прохождения**: `correctAnswers / totalQuestions × 100`.
- **totalPassedOwn** / **averageScoreOwn** — полностью пройденные тесты, где пользователь является автором (`Tests.AuthorId = userId`).
- **totalPassedOthers** / **averageScoreOthers** — полностью пройденные тесты других авторов.
- История (`GetUserResults`) — одна запись на `(UserId, TestId)`; после `DeleteResult` старая попытка не хранится.

### JWT-аутентификация

- Access token: 15 минут, хранится в памяти
- Refresh token: 7 дней, localStorage, ротация при обновлении
- При 401 → POST /auth/refresh → повтор запроса
- При ошибке обновления → выход

---

## 8. UI/UX

### Страницы

**Guest (standalone, `VITE_APP_MODE=guest`):**

- **Главная** — краткое описание, ссылки на разделы
- **Редактор** — создание теста, сохранение в localStorage, экспорт JSON
- **Импорт** — JSON из файла или буфера обмена
- **Мои тесты** — список из localStorage, прохождение, удаление

**Full (дополнительно к guest-функциям через API):**

- **Публичные тесты** — список с пагинацией
- **Вход / Регистрация**
- **Профиль** — статистика, история, облачные тесты

### Прохождение теста (TestPlayer)

**Состояния компонента:**
- `loading` — загрузка вопроса
- `answering` — выбор варианта ответа (radio buttons)
- `checking` — отправка ответа на сервер
- `result` — показ правильного/неправильного ответа с подсветкой
- `completed` — итоговый результат (score, percentage)
- `unresolved` — есть новые вопросы для дорешивания

**Навигация:**
- Один вопрос на экране
- Индикация вопросов: не отвечен / отвечен правильно / неправильно
- Кнопка «Проверить» → отправка ответа → подсветка
- Кнопка «Далее» → следующий вопрос
- Можно вернуться к предыдущим (результаты сохранены)
- После последнего вопроса → итоговый результат

### Редактор теста (TestEditor)

**Состояния:** `editing` → `preview` → `saving`

**Функции:**
- Добавление/редактирование вопросов и ответов (до сохранения)
- Отметка правильного ответа (ровно один на вопрос)
- Предпросмотр как пользователь
- Сохранение в localStorage или экспорт в JSON (гость)
- Сохранение на сервер (пользователь)

### Восстановление прогресса

- **Пользователь**: при входе `GET /result` → подсветить отвеченные вопросы
- **Гость**: localStorage хранит прогресс и ответы
- При перезагрузке страницы восстанавливается последнее состояние

### Хранение на клиенте (localStorage-ключи)

| Ключ | Содержимое |
|------|------------|
| `vibetest_local_tests` | Массив локальных тестов |
| `vibetest_progress_{id}` | Прогресс прохождения теста |
| `vibetest_player_explanation_settings` | Настройки показа пояснений: `{ showOnCorrect, showOnIncorrect }` |
| `vibetest_guest_results` | Результаты гостя |
| `vibetest_refresh_token` | JWT refresh token |

### JSON-формат для импорта/экспорта

```json
{
  "name": "SQL Basics",
  "description": "Test your knowledge",
  "questions": [{
    "text": "What does SQL stand for?",
    "answers": [
      "Structured Query Language",
      "Simple Query Logic"
    ],
    "correct": 0,
    "explanation": "SQL means Structured Query Language."
  }]
}
```

**Валидация импорта:**
- `name` непустое
- Минимум 1 вопрос
- Минимум 2 ответа на вопрос
- `correct` — валидный индекс в массиве `answers`
- `explanation` — необязательное пояснение; хранится в `Questions.Explanation` (per-test); клиент показывает после ответа (настройки видимости в localStorage)

---

## 9. Этапы разработки

Ниже — исторический план и текущее состояние репозитория (все перечисленные этапы **реализованы**).

**Этап 1 — Сервисы и БД** ✓

- `TestService`, `ResultService`, `UserService`, `ApplicationService`, `AuthService`
- Репозитории с пагинацией и агрегациями
- EF Core миграции в `VibeTest.Server/Data/Migrations/` (при старте — `Migrate()`, в `Testing` — `EnsureCreated()`)
- Интеграционные тесты сервисов в `VibeTest.Tests` на SQLite

| Сервис | Покрытые сценарии |
|--------|-------------------|
| TestService | CreateTest; AppendQuestions; GetTestDetail vs GetTestFull; cascade delete |
| ResultService | Submit + переответ; дорешивание после AppendQuestions; DeleteResult |
| UserService | GetStats, own/others passing stats |
| ApplicationService | link/internal заявки; hideResults; анонимное и персональное прохождение |

**Этап 2 — API** ✓

- `ServiceCollectionExtensions.AddVibeTestServices()` + `AddDbContext`
- Контроллеры: `AuthController`, `TestsController`, `ResultsController`, `ApplicationsController`
- JWT (access + refresh), `DomainExceptionMiddleware`
- Интеграционные тесты API (`WebApplicationFactory`)

**Этап 3 — Фронтенд-ядро** ✓

- **guest** — `types/`, `utils/`, `guest/GuestApp.tsx`, страницы и seed
- **full** — `full/api/client.ts`, `AuthContext`, роутинг и защищённые маршруты
- Скрипты: `dev`, `dev:full`, `build:guest`

**Этап 4 — Фронтенд-UI** ✓

- Guest: `TestEditor`, `TestPlayer`, импорт/экспорт, PWA
- Full: публичный каталог, профиль, облачные тесты, заявки (`/applications`, `/application/:token`)

**Этап 5 — E2E-тесты** ✓

- Playwright: `e2e/guest`, `e2e/full`
- CI: `.github/workflows/e2e.yml`

**Дальнейшие улучшения (не блокируют текущий MVP):**

- Production-раздача SPA из Kestrel или отдельного reverse proxy (см. [deployment.md](deployment.md))
- Секреты и CORS из переменных окружения, а не из `appsettings.json`
- Docker / docker-compose

### Деплой гостевого SPA на GitHub Pages

1. В настройках репозитория: Pages → Source: **GitHub Actions**
2. `VITE_BASE_PATH` в workflow должен совпадать с именем репозитория (`/vibe-test/` для `user/vibe-test`)
3. Workflow `.github/workflows/deploy-guest.yml`: `npm ci` → `npm run build:guest` → deploy `dist/`
4. Локальная проверка: `npm run build:guest && npm run preview`