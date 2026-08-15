# Руководство разработчика

## Интеграционные тесты (.NET)

```bash
cd VibeTest
dotnet test
```

Проект `VibeTest.Tests` содержит:

- тесты сервисов (`TestService`, `ResultService`, `UserService`, `ApplicationService`) на SQLite;
- тесты API через `WebApplicationFactory` (`Auth`, `Tests`, `Applications`, `Users`, rate limiting, health endpoints).

В среде `Testing` БД создаётся через `EnsureCreated()`; в остальных — `Migrate()`.

## E2E (Playwright)

Рабочая директория: `VibeTest/vibetest.client`.

```bash
npm run e2e:install   # установка Chromium (один раз)
npm run e2e:guest     # guest-режим
npm run e2e:full      # full: API с ASPNETCORE_ENVIRONMENT=E2E
npm run e2e           # оба набора подряд
```

Конфиги: `playwright.guest.config.ts`, `playwright.full.config.ts`.  
Full E2E перед запуском очищает `vibetest.e2e.db` (`e2e/full/global-setup.ts`).

Сборка для E2E:

```bash
npm run build:e2e-guest
npm run build:e2e
```

## Миграции Entity Framework

Из корня `VibeTest/`:

```bash
dotnet ef migrations add <ИмяМиграции> --project VibeTest.Server
dotnet ef database update --project VibeTest.Server
```

Миграции лежат в `VibeTest.Server/Data/Migrations/`. При обычном старте сервера `Migrate()` вызывается автоматически.

Design-time factory: `AppDbContextFactory.cs`.

## Seed-данные (guest)

Демо-тесты для guest состоят из:

- `src/guest/data/seed/meta.ts` — метаданные 11 тем;
- `src/guest/data/seed/questions/*.json` — банк вопросов по темам;
- `src/guest/data/seedTests.ts` — сборка `SEED_TESTS`.

Регенерация JSON из внешнего банка:

```bash
cd VibeTest/vibetest.client
npm run generate:seed
```

Скрипт: `scripts/generate-seed-data.mjs` (и связанный `scripts/seed-question-bank.mjs`).

Сид применяется в runtime: `seedLocalTests.ts` → только если `isGuestMode` и localStorage пуст.

## localStorage-ключи

Определены в `src/utils/storage.ts`:

| Ключ | Назначение |
|------|------------|
| `vibetest_local_tests` | массив локальных тестов |
| `vibetest_guest_results` | результаты прохождения локальных тестов |
| `vibetest_progress_{id}` | прогресс локального теста (uuid) |
| `vibetest_progress_api_{id}` | прогресс облачного теста (numeric id) |
| `vibetest_application_progress_{token}` | прогресс прохождения по ссылке заявки |

JWT-токены в full-режиме хранятся отдельно в `AuthContext` (не в этом файле).

## npm-скрипты

Из `package.json`:

| Скрипт | Описание |
|--------|----------|
| `dev` | Vite, режим guest → http://localhost:5173 |
| `dev:full` | Vite, режим full → https://localhost:64028, proxy `/api` |
| `build` | Production-сборка full |
| `build:guest` | Production-сборка guest |
| `build:e2e` | Сборка для full E2E |
| `build:e2e-guest` | Сборка для guest E2E |
| `preview` | Просмотр production-сборки |
| `preview:guest` | Просмотр guest-сборки |
| `lint` | ESLint |
| `e2e` | Playwright guest + full |
| `e2e:guest` | Только guest E2E |
| `e2e:full` | Только full E2E |
| `e2e:install` | `playwright install chromium` |
| `generate:seed` | Генерация seed JSON |

## Конфигурация сервера

`VibeTest.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=db/vibetest.db"
  },
  "Jwt": {
    "Key": "dev-secret-key-change-in-production-32chars!",
    "Issuer": "VibeTest",
    "Audience": "VibeTest",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "RateLimit": {
    "Global": { "PermitLimit": 100, "WindowSeconds": 60 },
    "AuthLogin": { "PermitLimit": 5, "WindowSeconds": 60 },
    "AuthRegisterRefresh": { "PermitLimit": 10, "WindowSeconds": 60 }
  }
}
```

Локальные Vite origin для CORS переопределяются в `appsettings.Development.json`. Для интеграционных тестов (`Testing`) лимит `AuthLogin` снижен до `3` запросов в минуту; тест `AuthRateLimitApiTests` проверяет ответ `429` на четвёртой попытке.

### Health и observability

- `GET /health/live` — liveness (без проверок зависимостей)
- `GET /health/ready` — readiness с проверкой SQLite через EF Core
- Serilog request logging обогащается полями `RequestHost`, `RequestScheme`, `RemoteIp`, `UserId`
- превышение rate limit логируется с методом, путём и partition key

Для локальной разработки можно переопределить значения в `appsettings.Development.json`.

### Логирование (Serilog)

Логи всегда пишутся в консоль. Persistent sink выбирается через `LogOutput:Destination`:

- `File` — файл `VibeTest.Server/logs/vibetest-<дата>.log` (rolling, 14 дней)
- `None` — только консоль

По умолчанию используется `File`; в `Testing` и `E2E` — `None`. Для локального переключения поменяйте `LogOutput:Destination` в `appsettings.Development.json` или задайте переменную окружения `LogOutput__Destination`.

В `Development` дополнительно логируются SQL-запросы EF Core (категория `Microsoft.EntityFrameworkCore.Database.Command`) с текстом запроса и значениями параметров. В production и тестовых средах SQL не логируется.

Все логи внутри одного HTTP-запроса связаны общим `RequestId` (`HttpContext.TraceIdentifier`): HTTP completion log, сервисные логи и SQL-запросы EF Core получают одинаковый ключ.

## Структура фронтенда (кратко)

| Путь | Назначение |
|------|------------|
| `src/config/env.ts` | `appMode`, `basePath`, `apiUrl` |
| `src/guest/` | GuestApp, страницы, layout |
| `src/full/` | FullApp, API-клиент, auth |
| `src/components/tests/` | `TestEditor`, `TestPlayer` |
| `src/utils/import.ts` | Валидация JSON |
| `src/utils/export.ts` | Экспорт JSON |
| `src/utils/importTemplate.ts` | Шаблон для `/info` |

## Полезные ссылки

- REST API: [api-reference.md](api-reference.md)
- Домен и DTO: [spec.md](spec.md)
- Запуск: [getting-started.md](getting-started.md)
