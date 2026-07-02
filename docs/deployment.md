# Деплой и CI

## Guest mode → GitHub Pages

Основной автоматический деплой — workflow [`.github/workflows/deploy-guest.yml`](../.github/workflows/deploy-guest.yml).

### Настройка репозитория

1. **Settings → Pages → Build and deployment**
2. **Source:** GitHub Actions (не «Deploy from a branch»)

### Как работает workflow

Триггеры: push в `main` / `master`, ручной запуск (`workflow_dispatch`).

1. `npm ci` и `npm run build:guest` в `VibeTest/vibetest.client`
2. `VITE_BASE_PATH=/${{ github.event.repository.name }}/` — для репозитория `vibe-test` это `/vibe-test/`
3. Артефакт `dist/` загружается и публикуется через `deploy-pages`

После публикации guest-приложение на GitHub Pages можно установить как PWA. Обновления подхватываются через баннер в интерфейсе после деплоя новой версии.

### Локальная проверка перед деплоем

```bash
cd VibeTest/vibetest.client
npm run build:guest
npm run preview:guest
```

Для имитации GitHub Pages задайте base path:

```bash
VITE_BASE_PATH=/vibe-test/ npm run build:guest
```

Файл [`.env.guest.production`](../VibeTest/vibetest.client/.env.guest.production):

```
VITE_APP_MODE=guest
VITE_BASE_PATH=/vibe-test/
```

Замените `/vibe-test/` на `/имя-вашего-репозитория/`, если имя отличается.

---

## Full mode (production)

### Ограничение текущего кода

Статика SPA из `dist/` отдаётся сервером **только в среде Development**:

```37:42:VibeTest/VibeTest.Server/WebApplicationExtensions.cs
        if (app.Environment.IsDevelopment())
        {
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.MapFallbackToFile("/index.html");
        }
```

В `Production` и `Staging` Kestrel обслуживает только API; отдельной раздачи `index.html` нет.

### Рекомендуемый путь для production

1. Собрать фронтенд:
   ```bash
   cd VibeTest/vibetest.client
   npm run build
   ```
2. Разместить содержимое `dist/` так, чтобы reverse proxy или веб-сервер отдавал SPA, а `/api` проксировал на Kestrel.
3. Задать `VITE_API_URL` при сборке, если API на другом origin (иначе по умолчанию `/api`).
4. **Обязательно сменить** в production:
   - `Jwt:Key` в конфигурации (минимум 32 символа)
   - `ConnectionStrings:DefaultConnection` — путь к SQLite или другая СУБД при миграции

Пример `appsettings.Production.json` (создаётся вручную на сервере):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/vibetest/vibetest.db"
  },
  "Jwt": {
    "Key": "<случайная-строка-32+-символов>"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://your-spa.example.com"
    ]
  },
  "RateLimit": {
    "Global": {
      "PermitLimit": 100,
      "WindowSeconds": 60
    },
    "AuthLogin": {
      "PermitLimit": 5,
      "WindowSeconds": 60
    },
    "AuthRegisterRefresh": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    }
  }
}
```

Те же ключи можно задать через переменные окружения, например `Cors__AllowedOrigins__0=https://your-spa.example.com`.

### CORS

Разрешённые origin SPA задаются в `Cors:AllowedOrigins`. В `Development` и `E2E` уже прописаны локальные Vite-адреса; в production список **обязателен** — в базовом `appsettings.json` он пуст.

### Rate limiting

Встроенный in-memory rate limiter ASP.NET Core:

| Политика | Маршруты | Назначение |
|----------|----------|------------|
| `global-api` | все `/api/*` | общий лимит; ключ — user id (если авторизован) или IP |
| `auth-login` | `POST /api/auth/login` | защита от перебора пароля |
| `auth-register-refresh` | `POST /api/auth/register`, `POST /api/auth/refresh` | снижение злоупотреблений регистрацией и refresh |

При превышении лимита API возвращает **429 Too Many Requests** и заголовок `Retry-After`. Отклонённые запросы пишутся в лог (`VibeTest.RateLimiting`) с методом, путём и partition key.

Лимитер хранится в памяти процесса — при нескольких инстансах API перенесите ограничение на reverse proxy или распределённый store.

### Health checks

| Endpoint | Назначение |
|----------|------------|
| `GET /health/live` | liveness — процесс отвечает |
| `GET /health/ready` | readiness — проверка доступности БД (EF Core) |

Настройте пробы reverse proxy / оркестратора на эти URL. Health endpoints **не** попадают под rate limiting.

Запуск API:

```bash
cd VibeTest
ASPNETCORE_ENVIRONMENT=Production dotnet run --project VibeTest.Server
```

### Docker

В репозитории **нет** Dockerfile и docker-compose — готовых контейнерных инструкций нет.

---

## CI: E2E-тесты

Workflow [`.github/workflows/e2e.yml`](../.github/workflows/e2e.yml) на каждый push/PR в `main` / `master`:

1. Устанавливает .NET 10 и Node.js 22
2. `npm ci` + Playwright Chromium
3. `npm run e2e:guest` — guest E2E
4. `npm run e2e:full` — full E2E (API поднимается с `ASPNETCORE_ENVIRONMENT=E2E`)

При падении загружается артефакт `playwright-report` (хранение 7 дней).

Локальный запуск тех же тестов:

```bash
cd VibeTest/vibetest.client
npm run e2e:install    # один раз
npm run e2e:guest
npm run e2e:full
```

---

## Переменные окружения (сводка)

| Переменная | Где | Назначение |
|------------|-----|------------|
| `VITE_APP_MODE` | сборка SPA | `guest` / `full` |
| `VITE_BASE_PATH` | сборка SPA | базовый путь (GitHub Pages) |
| `VITE_API_URL` | сборка full SPA | URL API (по умолчанию `/api`) |
| `ASPNETCORE_ENVIRONMENT` | сервер | `Development`, `Production`, `E2E`, `Testing` |
| `ConnectionStrings:DefaultConnection` | appsettings | SQLite |
| `Jwt:*` | appsettings | ключ, issuer, сроки токенов |
| `Cors:AllowedOrigins` | appsettings / env | разрешённые SPA origin |
| `RateLimit:*` | appsettings / env | лимиты запросов (см. выше) |

Подробнее о разработке: [development.md](development.md)
