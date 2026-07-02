# Быстрый старт

## Требования

| Инструмент | Версия |
|------------|--------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10 |
| [Node.js](https://nodejs.org/) | 22 |
| npm | поставляется с Node.js |

Для full-режима в dev также используется ASP.NET dev certificate (Vite создаёт его автоматически через `dotnet dev-certs`).

## Guest mode (без бэкенда)

Подходит для работы с локальными тестами, импортом/экспортом JSON и страницей `/info`.

```bash
cd VibeTest/vibetest.client
npm ci
npm run dev
```

Приложение откроется на **http://localhost:5173**.

Переменные окружения берутся из `.env.guest`:

```
VITE_APP_MODE=guest
VITE_BASE_PATH=/
```

При первом запуске в пустой localStorage автоматически появятся 11 демо-тестов.

## Full mode (фронтенд + API)

Нужны **два терминала** (или один `dotnet run` со SpaProxy — см. ниже).

### Вариант A: два терминала

**Терминал 1 — API:**

```bash
cd VibeTest
dotnet run --project VibeTest.Server
```

API по умолчанию слушает `https://localhost:7215` и `http://localhost:5032`. При старте применяются миграции EF к файлу `VibeTest.Server/db/vibetest.db`.

**Терминал 2 — фронтенд:**

```bash
cd VibeTest/vibetest.client
npm run dev:full
```

Фронтенд: **https://localhost:64028**. Запросы к `/api` проксируются на Kestrel (см. `vite.config.ts`).

Переменные из `.env.full`:

```
VITE_APP_MODE=full
VITE_BASE_PATH=/
VITE_API_URL=/api
```

### Вариант B: dotnet run со SpaProxy

В `VibeTest.Server.csproj` настроен SpaProxy: при `dotnet run` сервер сам запускает `npm run dev` и открывает фронтенд на `https://localhost:64028`.

```bash
cd VibeTest
dotnet run --project VibeTest.Server
```

Профили запуска — в `Properties/launchSettings.json` (`https`, `http`).

## Первые шаги

### Guest

1. Откройте `/tests` — список демо-тестов или пустой список.
2. Создайте тест в `/editor` или импортируйте JSON на `/import`.
3. Посмотрите формат на `/info`.
4. Пройдите тест через `/play/:id`.

### Full

1. Зарегистрируйтесь на `/register` или войдите на `/login`.
2. Создайте облачный тест в `/editor` — он появится в `/my/tests` на вкладке «Облачные».
3. Опубликуйте тест — он станет виден в `/tests`.
4. Пройдите чужой тест на `/tests/:id/play`.
5. История и статистика — в `/profile`.

Локальные тесты (как в guest) доступны на `/my/tests` (вкладка «Локальные»); импорт и `/info` работают так же.

## Сборка для проверки

```bash
# Guest
cd VibeTest/vibetest.client
npm run build:guest
npm run preview:guest

# Full
npm run build
npm run preview
# API отдельно: dotnet run --project ../VibeTest.Server
```

## Дальше

- Деплой на GitHub Pages и production: [deployment.md](deployment.md)
- Тесты, миграции, seed: [development.md](development.md)
