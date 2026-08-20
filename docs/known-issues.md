# Известные проблемы и технический долг

Документ фиксирует результаты аудита проекта (август 2026). Проблемы сгруппированы по приоритету и области. Статус «открыта» означает, что исправление ещё не внесено в код.

## Сводка

| Область | Оценка | Комментарий |
|---------|--------|-------------|
| Архитектура | 8/10 | Чёткое разделение guest/full, слоистый backend |
| Frontend | 7/10 | Хорошая структура, но слабая compile-time типизация |
| Backend | 6/10 | Solid MVP, есть пробелы в авторизации и безопасности |
| Тесты | 7/10 | ~73 integration + Playwright E2E, мало unit-тестов |
| Production-ready | 5/10 | Guest на GitHub Pages работает; full-mode требует доработок |

---

## Критично (P0)

### 1. Submit к чужим private-тестам

**Статус:** открыта  
**Область:** backend, безопасность

`ResultService.SubmitAnswer` проверяет только существование теста, но не `IsPublic` и не ownership автора. Любой авторизованный пользователь может отправлять ответы к приватному тесту, зная его `testId`.

**Файлы:** `VibeTest.Server/Services/ResultService.cs` (строки 27–28)

**Рекомендация:** разрешать submit только если `test.IsPublic == true` или `test.AuthorId == userId`, либо через flow заявок (applications).

---

### 2. Публичный endpoint раскрывает правильные ответы

**Статус:** открыта  
**Область:** backend, безопасность

`GET /api/tests/{id}/play-public` возвращает `TestFullResponse` с полем `Correct` у каждого вопроса. Любой анонимный пользователь может получить все правильные ответы до прохождения теста.

**Файлы:** `VibeTest.Server/Services/TestService.cs` (`GetPublicPlayTest`, строки 235–253)

**Рекомендация:** для прохождения отдавать `QuestionDetailDto` без `Correct`; полный payload — только автору через `/full`.

---

### 3. JWT-ключ в репозитории

**Статус:** открыта  
**Область:** backend, секреты

Dev-ключ JWT хранится в базовом `appsettings.json` и коммитится в git. При деплое full-mode без переопределения через env/secrets все токены могут быть скомпрометированы.

**Файлы:** `VibeTest.Server/appsettings.json` (строки 5–10)

**Рекомендация:** вынести `Jwt:Key` в User Secrets / переменные окружения; fail-fast при старте, если в non-Development используется дефолтный ключ.

---

### 4. Отсутствует `.env.e2e` для full E2E

**Статус:** открыта  
**Область:** CI, тесты

Есть `.env.e2e-guest`, `.env.guest`, `.env.full`, но нет `.env.e2e`. Сборка `build:e2e` использует `--mode e2e`; без env-файла `VITE_APP_MODE` по умолчанию — `guest`. Full E2E может собираться в guest-режиме и давать ложное покрытие или падать в CI.

**Файлы:** `vibetest.client/playwright.full.config.ts`, `package.json` (`build:e2e`)

**Рекомендация:** добавить `.env.e2e`:

```env
VITE_APP_MODE=full
VITE_BASE_PATH=/
VITE_API_URL=http://localhost:5032/api
```

---

## Высокий приоритет (P1)

### 5. GitHub Pages: 404 на deep links

**Статус:** открыта  
**Область:** frontend, деплой

При прямом переходе на URL вроде `/vibe-test/tests` или при refresh GitHub Pages ищет физический файл и возвращает 404. После первого открытия корня service worker (PWA) подменяет навигацию на `index.html`, и ошибка исчезает.

**Файлы:** `vibetest.client/src/guest/GuestApp.tsx`, `vite.config.ts` (workbox `navigateFallback`), отсутствие `public/404.html`

**Рекомендация:** `HashRouter` (простой вариант) или post-build `404.html` (= `index.html`) для GitHub Pages.

---

### 6. TypeScript `strict` не включён

**Статус:** открыта  
**Область:** frontend, типизация

В `tsconfig.app.json` есть `noUnusedLocals` / `noUnusedParameters`, но нет `"strict": true`. Ошибки вроде `TestDifficulty | undefined is not assignable to TestDifficulty` не ловятся при `npm run dev`, только при `npm run build:guest`.

**Файлы:** `vibetest.client/tsconfig.app.json`, `src/components/tests/LocalTestsList.tsx:108`

**Рекомендация:** включить `"strict": true`, исправить возникшие ошибки; перед push запускать `npm run build:guest`.

---

### 7. `JSON.parse` без защиты в LocalTestsList

**Статус:** открыта  
**Область:** frontend, надёжность

При повреждённом `localStorage` `JSON.parse(raw)` в `LocalTestsList` может упасть и показать белый экран. В `storage.ts` есть fallback через `readJson`, но snapshot читается напрямую.

**Файлы:** `vibetest.client/src/components/tests/LocalTestsList.tsx` (строки 51–52)

**Рекомендация:** парсить через `readJson` / `getLocalTests()` или обернуть в try/catch с fallback `[]`.

---

### 8. Guest-бандл тянет full API/auth

**Статус:** открыта  
**Область:** frontend, производительность

Guest-сборка статически импортирует код full-режима через общие компоненты (`TestEditor`, `playerSources`, `AuthContext`). Для offline guest PWA это лишний JS без функциональной пользы.

**Файлы:** `src/components/tests/player/playerSources.ts`, `src/components/tests/TestEditor.tsx`, `src/guest/pages/EditorPage.tsx`

**Рекомендация:** вынести `getApiErrorMessage` в `utils/errors.ts`; dynamic import API-модулей только в full paths; lazy routes для full-only страниц.

---

### 9. Race condition при submit (public tests)

**Статус:** открыта  
**Область:** backend, конкурентность

`ResultService.SubmitAnswer` выполняет check → insert → aggregate update без транзакции. При параллельных submit возможен необработанный 500 из-за unique index или lost update на `UserTestResult`. Для applications submit уже обёрнут в транзакцию.

**Файлы:** `VibeTest.Server/Services/ResultService.cs` (строки 36–69)

**Рекомендация:** транзакция + обработка unique violation → 400, по образцу `ApplicationRepository.SubmitAnswerAsync`.

---

### 10. `dotnet test` не в CI

**Статус:** открыта  
**Область:** CI

Workflow `e2e.yml` запускает только Playwright. Интеграционные тесты backend (~73) не выполняются на push/PR.

**Файлы:** `.github/workflows/e2e.yml`

**Рекомендация:** добавить job `dotnet test VibeTest/` в CI или отдельный workflow.

---

### 11. Production full-mode: SPA не отдаётся Kestrel

**Статус:** открыта (осознанное ограничение)  
**Область:** деплой

В Production Kestrel не раздаёт статику SPA — только API. Reverse proxy или отдельный веб-сервер обязателен.

**Файлы:** `VibeTest.Server/WebApplicationExtensions.cs` (строки 74–79), `docs/deployment.md`

**Рекомендация:** Dockerfile + nginx/Caddy; задокументировать checklist (JWT, CORS, SPA path).

---

### 12. CORS пуст в base config

**Статус:** открыта  
**Область:** backend, деплой

`appsettings.json` содержит `"AllowedOrigins": []`. Политика CORS применяется только если origins не пуст. Cross-origin SPA + API в production без env-override не заработает.

**Файлы:** `VibeTest.Server/appsettings.json`, `ServiceCollectionExtensions.cs`

---

## Средний приоритет (P2)

### 13. Нет React Error Boundaries

Любая необработанная ошибка render (corrupt localStorage, invalid API JSON) приводит к белому экрану без recovery UI.

**Рекомендация:** Error Boundary на уровне route layout.

---

### 14. Нет code splitting / lazy routes

Все страницы и seed JSON (~76 KB) попадают в initial bundle. `React.lazy` / `Suspense` не используются.

**Файлы:** `src/guest/data/seedTests.ts`, `GuestApp.tsx`

---

### 15. Race conditions в списках без request guard

`PublicTestsPage` и `MyTestsPage` — `loadPage` без `requestId`/`cancelled` (в отличие от `ApplicationsPage`). Быстрая смена страницы/сортировки может применить устаревший ответ.

---

### 16. API client: `JSON.parse` без обработки невалидного JSON

При `response.ok` и не-JSON теле — необработанный `SyntaxError`, не `ApiError`.

**Файлы:** `src/full/api/client.ts` (строки 73–79)

---

### 17. Хрупкая обработка duplicate answer по тексту сообщения

`isAlreadyAnsweredError` сравнивает строку с константой. Смена текста ошибки на backend сломает restore-логику в плеере.

**Файлы:** `src/components/tests/player/playerSources.ts`, `useTestPlayerController.ts`

**Рекомендация:** определять duplicate по HTTP status/code, не по тексту.

---

### 18. Refresh token в localStorage (XSS-риск)

Стандартная SPA-модель: при XSS refresh token может быть украден. Access token только в памяти — плюс.

**Файлы:** `src/utils/authStorage.ts`, `src/full/context/AuthContext.tsx`

---

### 19. Нет logout / revoke refresh tokens

Украденный refresh token живёт до expiry (7 дней). Нет endpoint для отзыва токенов.

**Файлы:** `VibeTest.Server/Services/AuthService.cs`

---

### 20. Race на duplicate email при register

Check-then-insert без транзакции; unique index есть, но `DbUpdateException` не обрабатывается → возможен 500 вместо 400.

**Файлы:** `VibeTest.Server/Services/AuthService.cs`

---

### 21. Необработанные исключения → default 500

`DomainExceptionMiddleware` ловит только `DomainException`. Остальное — default 500 (в dev возможен stack trace).

**Файлы:** `VibeTest.Server/Middleware/DomainExceptionMiddleware.cs`

---

### 22. ~~Глобальный пул Question/Answer~~ (исправлено)

Ранее вопросы и ответы дедуплицировались глобально по `Text`. Сейчас каждый вопрос и ответ принадлежит конкретному тесту (`Tests → Questions → Answers`).

---

### 23. Нет MaxLength на строковые поля

Риск storage abuse через длинные строки в entities.

---

### 24. ESLint не в CI

`npm run lint` есть в `package.json`, но в GitHub Actions не вызывается.

---

### 25. Deploy guest без quality gate

`deploy-guest.yml` деплоит на push в main независимо от результата E2E. Теоретически можно задеплоить при падающих тестах.

**Рекомендация:** `deploy-guest` → `needs: [e2e]` или required checks в GitHub.

---

### 26. Документация расходится с реальностью

- `spec.md` — только 2 контроллера в описании; в коде 5 (в т.ч. `UsersController`)
- `spec.md` — только `deploy-guest.yml`; есть также `e2e.yml`
- `getting-started.md:68` — «SpaProxy запускает `npm run dev`»; в csproj — `dev:full`
- `vibetest.client/README.md` — шаблон Vite, не описывает dual-mode

---

### 27. Нет unit-тестов frontend

Только Playwright E2E. Регрессии в utils (`import.ts`, `validateTest.ts`, `storage.ts`, player logic) ловятся только E2E.

---

### 28. `TestEditor.tsx` — монолит ~536 строк

Смешаны load/save, CRUD вопросов, preview, API/local modes, pagination.

---

## Низкий приоритет (P3)

| # | Проблема | Файлы / комментарий |
|---|----------|---------------------|
| 29 | Дублирование `copyToClipboard` | `ApplicationsPage.tsx` vs `utils/clipboard.ts` |
| 30 | `window.confirm` для удаления | `MyTestsPage.tsx` — блокирующий UX |
| 31 | Inline styles | разбросаны по страницам |
| 32 | PWA dev отключён | SW тестируется только на production build |
| 33 | `document.execCommand('copy')` fallback | deprecated API в `clipboard.ts` |
| 34 | ESLint без type-aware rules | `eslint.config.js` — только `recommended` |
| 35 | Нет LICENSE, CONTRIBUTING, Dependabot | корень репозитория |
| 36 | Нет pin версий toolchain | нет `global.json`, `.nvmrc`, `engines` |
| 37 | OpenAPI только в Development | `/openapi` недоступен в production |
| 38 | In-memory rate limiter | при горизонтальном масштабировании лимиты не согласованы |
| 39 | `GetApplicationResultById` без HTTP endpoint | мёртвый API surface |
| 40 | Publish без проверки наличия вопросов | `TestService.cs` |
| 41 | `SubmitResponse` сразу отдаёт `CorrectAnswerOrder` | облегчает cheating в exam-сценарии |
| 42 | Корневой README минимален | 8 строк, без badges/CI status |

---

## Рекомендуемый порядок исправлений

1. **Безопасность backend** — закрыть submit к private-тестам, убрать утечку ответов через `play-public`, вынести JWT key.
2. **CI** — добавить `.env.e2e`, `dotnet test`, связать deploy с E2E.
3. **Frontend надёжность** — `strict: true`, защита `LocalTestsList`, Error Boundary.
4. **GitHub Pages** — deep links (`HashRouter` или `404.html`).
5. **Backend hardening** — транзакции submit, global exception handler, CORS для prod.
6. **Документация** — обновить `spec.md`, `getting-started.md`, client README.
7. **Производительность** — code splitting, развязка guest от full API.
8. **Unit-тесты** — `import.ts`, `validateTest.ts`, `storage.ts`, AuthService.

---

## Связанные документы

- [deployment.md](deployment.md) — деплой и известные ограничения production
- [development.md](development.md) — локальные тесты и сборка
- [spec.md](spec.md) — техспецификация (частично устарела, см. п. 26)
