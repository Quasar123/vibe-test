# VibeTest

Конструктор и прохождение тестов в браузере. В **guest**-режиме приложение работает автономно (localStorage, импорт/экспорт JSON); в **full**-режиме добавляются регистрация, облачное хранение тестов, публикация и история прохождений на ASP.NET Core + PostgreSQL.

Стек: React + TypeScript + Vite (`vibetest.client`), ASP.NET Core 10 (`VibeTest.Server`).

**Документация:** [docs/README.md](docs/README.md)

## PostgreSQL (full mode)

```bash
# только БД (PostgreSQL доступен на localhost:5432)
docker compose -f docker/compose.infra.yml up -d
cd VibeTest && dotnet run --project VibeTest.Server
```

## Полный стек в Docker

```bash
docker compose -f docker/compose.app.yml up -d --build
# SPA + API: http://localhost:8080
# PostgreSQL для мониторинга: localhost:5432
```
