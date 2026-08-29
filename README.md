# Central Wiki MCP Server

Централизованный MCP-сервер на .NET 10, дающий harness-ам и другим LLM-клиентам
доступ к корпоративной wiki через протокол MCP — без копирования wiki на машины клиентов.

Реализовано по BRD [`spec.md`](spec.md) (v0.2).

```
Harness / LLM client
        │  MCP (Streamable HTTP)
        ▼
Central Wiki MCP Server ──► PostgreSQL (индекс + аудит)
        │
        └──► источник wiki (папка с Markdown)
```

## Что уже работает

| Инструмент MCP | Назначение |
|---|---|
| `wiki_search` | Полнотекстовый поиск с фильтрами по разделу, тегам и дате |
| `wiki_get_page` | Страница в Markdown, крупные — порциями |
| `wiki_list_pages` | Навигация по структуре wiki |

Плюс: аутентификация (JWT и API-key), ACL по разделам, аудит обращений,
двухуровневый rate limiting, health-эндпоинты, фоновая синхронизация индекса
и stdio-прокси для harness, умеющих только запускать локальный процесс.

## Быстрый старт

### Через Docker Compose

```bash
cp .env.example .env       # подставьте свои секреты
docker compose up --build
```

Сервис поднимется на `http://localhost:8080`, PostgreSQL — на `5433`, Redis — на `6379`.

### Локально

Нужен PostgreSQL. Проще всего поднять контейнером:

```bash
docker run -d --name wikimcp-pg \
  -e POSTGRES_USER=wikimcp -e POSTGRES_PASSWORD=wikimcp -e POSTGRES_DB=wikimcp \
  -p 5433:5432 postgres:17-alpine
```

Затем:

```bash
export WIKIMCP_Database__ConnectionString="Host=127.0.0.1;Port=5433;Database=wikimcp;Username=wikimcp;Password=wikimcp"
export WIKIMCP_WikiSource__RootPath="$PWD/wiki-content"
export WIKIMCP_ApiKeyAuth__Clients__0__ClientId="harness-local"
export WIKIMCP_ApiKeyAuth__Clients__0__Key="local-test-key-123"
export WIKIMCP_ApiKeyAuth__Clients__0__Groups__0="harness"

dotnet run --project src/CentralWikiMcp.Server
```

Миграции применяются на старте, wiki из `wiki-content/` индексируется автоматически.

Проверка:

```bash
curl http://localhost:5080/health/full | jq
```

## Подключение harness

### Вариант 1 — прямое HTTP-подключение

```json
{
  "mcpServers": {
    "corporate-wiki": {
      "type": "http",
      "url": "https://wiki-mcp.corp.internal/mcp",
      "headers": { "X-Api-Key": "<ключ сервисного аккаунта>" }
    }
  }
}
```

Вместо API-ключа можно передавать `Authorization: Bearer <JWT>`.

### Вариант 2 — stdio-прокси (UC-5)

Для harness, который умеет запускать только локальный процесс. Wiki при этом
на машине по-прежнему не хранится — прокси лишь переадресует вызовы.

```json
{
  "mcpServers": {
    "corporate-wiki": {
      "command": "wiki-mcp-proxy",
      "env": {
        "WIKI_MCP_SERVER_URL": "https://wiki-mcp.corp.internal/mcp",
        "WIKI_MCP_API_KEY": "<ключ сервисного аккаунта>"
      }
    }
  }
}
```

Сборка прокси:

```bash
dotnet publish src/CentralWikiMcp.StdioProxy -c Release -o ./proxy
```

## HTTP-эндпоинты

| Метод | Путь | Кто | Назначение |
|---|---|---|---|
| POST | `/mcp` | аутентифицированные | MCP-транспорт |
| GET | `/health` | все | liveness, зависимости не проверяются |
| GET | `/health/full` | все | readiness: БД и состояние синхронизации |
| GET | `/health/zabbix` | все | `1` или `0` для шаблона мониторинга |
| POST | `/api/v1/admin/sync?full=true` | `wiki-admins` | Ручная синхронизация и полный реиндекс |
| GET | `/api/v1/admin/sync/status` | `wiki-admins` | Состояние последней синхронизации |
| GET | `/api/v1/audit?from&to&take` | `wiki-auditors` | Выгрузка аудита |

## Формат wiki

Источник — папка с Markdown. Раздел определяется первой папкой в пути,
метаданные можно задать YAML-заголовком:

```markdown
---
title: Deployment runbook
namespace: runbooks
tags: [deploy, harness]
updatedAt: 2026-08-01T10:15:00Z
---

# Развёртывание сервиса
```

`namespace` — то, на чём стоит разграничение доступа (`AccessPolicy` в конфигурации).

## Разграничение доступа

Политика запрещающая по умолчанию: без явного разрешения раздел недоступен.

```json
"AccessPolicy": {
  "FullAccessGroups": ["wiki-admins"],
  "PublicNamespaces": ["general"],
  "NamespaceGroups": {
    "runbooks": ["harness", "developers"],
    "internal": ["developers"]
  }
}
```

Анонимный доступ к контенту закрыт всегда.

## Конфигурация

Несекретные параметры — в `appsettings.json`. Секреты только через переменные
окружения с префиксом `WIKIMCP_` (в dev их читает стаб Vault, в prod — Kubernetes Secrets).
Двойное подчёркивание разделяет уровни: `WIKIMCP_Database__ConnectionString`.

Основные секции: `WikiSource`, `Sync`, `WikiTools` (лимиты выдачи),
`AccessPolicy`, `RateLimiting`, `Jwt`, `ApiKeyAuth`. Полный список — в `.env.example`.

## Структура решения

```
src/
  CentralWikiMcp.Domain/          модели и порты (IWikiSource, IWikiIndex, IAccessPolicy…)
  CentralWikiMcp.Infrastructure/  файловый источник, индекс на PostgreSQL, аудит, синхронизация
  CentralWikiMcp.Server/          ASP.NET Core, MCP-инструменты, аутентификация, админ-API
  CentralWikiMcp.StdioProxy/      локальный stdio→HTTP прокси (UC-5)
  stubs/                          заглушки корпоративных пакетов, см. ниже
tests/CentralWikiMcp.UnitTests/   юнит-тесты
deploy/k8s/                       манифесты развёртывания
```

Смена источника wiki (Git, Confluence) — это новая реализация `IWikiSource`;
ни клиенты, ни остальной код при этом не меняются.

## Заглушки корпоративных пакетов

Спецификация требует пакеты `Abdt.*` и `Protoobp.Trace.Bundle` из внутреннего
Artifactory. Он недоступен вне корпоративной сети, поэтому в `src/stubs/` лежат
заглушки с **теми же именами сборок, пространств имён и методов расширения**:
`AddAbdtLogger()`, `AddRequestLogging()`, `AddAutoHealthChecks()`,
`UseAutoHealthChecks()`, `AddRateLimiting()`, `UseRateLimiting()`,
`[RateLimit]`, `AddVault()`, `WithAbdtValidation()`,
`AddOpenTelemetrySystemMetrics()`, `AddProtoobpTracing()`.

Внутри — рабочие реализации на Serilog, OpenTelemetry и StackExchange.Redis,
воспроизводящие требуемое спецификацией поведение: маскирование ПД по умолчанию,
sliding-window два уровня с fail-open, `/health`, `/health/full`, `/health/zabbix`.

**Переход на настоящие пакеты в корпоративном контуре:**

1. В `NuGet.config` включить источник Artifactory и убрать `nuget.org` (NFR-13).
2. В `Directory.Packages.props` добавить версии пакетов `Abdt.*` и `Protoobp.Trace.Bundle`.
3. В `CentralWikiMcp.Server.csproj` и `CentralWikiMcp.Infrastructure.csproj`
   заменить `ProjectReference` на заглушки соответствующими `PackageReference`.
4. Удалить каталог `src/stubs/`.

Вызывающий код править не нужно — сигнатуры совпадают.

## Разработка

```bash
dotnet build                                              # сборка решения
dotnet run --project tests/CentralWikiMcp.UnitTests       # тесты
dotnet ef migrations add <Name> \
  --project src/CentralWikiMcp.Infrastructure \
  --startup-project src/CentralWikiMcp.Infrastructure \
  --output-dir Persistence/Migrations
```

Сборка идёт с `TreatWarningsAsErrors` и включёнными анализаторами (NFR-07),
версии пакетов централизованы в `Directory.Packages.props` (NFR-06).

## Что не реализовано

Требования уровня Could из BRD: `wiki_ask`, `wiki_get_section`,
`wiki_recent_changes`, semantic/vector search. Точки расширения для них заложены
(`IWikiIndex`, `IWikiSource`), но LLM-провайдер для `wiki_ask` спецификацией не определён.

Подробнее о соответствии требованиям — [`docs/traceability.md`](docs/traceability.md).
