# Соответствие требованиям BRD

Сверка реализации с [`../spec.md`](../spec.md) (v0.2). Объём согласован как
**все Must + ключевые Should**; требования Could вынесены в отдельный раздел.

Обозначения: ✅ реализовано · ⚠️ реализовано с оговоркой · ⛔ не реализовано.

## Бизнес-требования

| ID | Требование | Статус | Где |
|---|---|---|---|
| BR-01 | Единый централизованный сервис | ✅ | `CentralWikiMcp.Server` |
| BR-02 | Harness не хранит копию wiki | ✅ | HTTP-транспорт; прокси только переадресует |
| BR-03 | Актуальный контент | ✅ | `WikiSyncBackgroundService` по расписанию |
| BR-04 | Защищённый доступ | ✅ | JWT + API-key, fallback-политика на весь конвейер |
| BR-05 | Логирование всех обращений | ✅ | `RequestLoggingMiddleware`, `ToolAuditor` |
| BR-06 | Реализация на .NET | ✅ | .NET 10, ASP.NET Core |
| BR-07 | Совместимость с MCP-клиентами | ✅ | `ModelContextProtocol.AspNetCore` 2.2.0 |
| BR-08 | Поиск по wiki | ✅ | `wiki_search` на PostgreSQL FTS |
| BR-09 | Чтение конкретных страниц | ✅ | `wiki_get_page` |
| BR-10 | Ограничение доступа по группам | ✅ | `NamespaceAccessPolicy` |
| BR-11 | Горизонтальное масштабирование | ✅ | Stateless-режим, счётчики лимитов в Redis |
| BR-12 | Несколько источников в будущем | ✅ | Порт `IWikiSource` |
| BR-13 | Расширение до RAG | ✅ | Порт `IWikiIndex` |

## Функциональные требования

### MCP-сервер

| ID | Статус | Комментарий |
|---|---|---|
| FR-01 | ✅ | `ModelContextProtocol.AspNetCore` |
| FR-02 | ✅ | Streamable HTTP; сессия stateless |
| FR-03 | ✅ | `tools/list` отдаёт три инструмента со схемами |
| FR-04 | ✅ | FluentValidation + `ArgumentNullException.ThrowIfNull` |
| FR-05 | ✅ | Структурированные JSON-ответы, ошибки — `application/problem+json` |
| FR-06 | ✅ | `ErrorHandling`, доменные ошибки → `McpException` с текстом |
| FR-07 | ✅ | `/health`, `/health/full`, `/health/zabbix` |
| FR-08 | ✅ | `/health/full` проверяет БД и синхронизацию |
| FR-09 | ✅ | `Asp.Versioning.Http`, префикс `/api/v1` |

### Инструменты

| ID | Статус | Комментарий |
|---|---|---|
| FR-10 | ✅ | Ранжирование `ts_rank`, заголовок весом A, тело B |
| FR-11 | ✅ | В индекс уходит только список разрешённых разделов |
| FR-12 | ✅ | `Math.Clamp(topK, 1, MaxTopK)` |
| FR-13 | ✅ | `pageId`, `path`, `title`, `snippet`, `score`, `updatedAt`, `revision` |
| FR-14 | ✅ | `tsvector` с GIN-индексом, конфигурация `russian` |
| FR-20 | ✅ | Поиск по `pageId` либо `path` |
| FR-21 | ✅ | Права проверяются до выдачи содержимого |
| FR-22 | ✅ | Markdown |
| FR-23 | ✅ | `revision` — хеш содержимого |
| FR-24 | ✅ | Порции по `PageChunkChars`, поля `truncated`/`nextOffset` |
| FR-25 | ✅ | Индексируются только расширения из `WikiSource:Extensions` |

### Синхронизация

| ID | Статус | Комментарий |
|---|---|---|
| FR-30 | ✅ | `FileSystemWikiSource` |
| FR-31 | ✅ | `WikiSyncBackgroundService` |
| FR-32 | ✅ | `Sync:IntervalMinutes` |
| FR-33 | ✅ | `POST /api/v1/admin/sync` |
| FR-34 | ✅ | `sync_states`, `GET /api/v1/admin/sync/status` |
| FR-35 | ✅ | Ошибка попадает в лог, состояние и `/health/full` (Degraded) |
| FR-36 | ✅ | Страницы с неизменной ревизией не переписываются |
| FR-37 | ✅ | `POST /api/v1/admin/sync?full=true` |

### Аутентификация и авторизация

| ID | Статус | Комментарий |
|---|---|---|
| FR-40 | ✅ | Fallback-политика требует аутентификации везде |
| FR-41 | ✅ | `AddJwtBearer` с проверкой issuer/audience/lifetime/signature |
| FR-42 | ✅ | `ApiKeyAuthenticationHandler`, сравнение за постоянное время |
| FR-43 | ✅ | `SubjectKind`: User / ServiceAccount / Anonymous |
| FR-44 | ✅ | ACL по разделам, запрет по умолчанию |
| FR-45 | ✅ | 401 без аутентификации, 403 без прав — через `TypedResults` |
| FR-46 | ✅ | Секреты только из окружения / Vault |
| FR-47 | ✅ | `WithAbdtValidation()` + `ValidateOnStart()` |

### Логирование и аудит

| ID | Статус | Комментарий |
|---|---|---|
| FR-50 | ✅ | Логируются и HTTP-запросы, и вызовы инструментов |
| FR-51 | ✅ | `BsnOperationId`, `RequestId`, `Origin`, subject, хеш параметров |
| FR-52 | ✅ | Маскирование по именам свойств и ПД в свободном тексте |
| FR-53 | ✅ | Таблица `audit_events` отдельно от логов |
| FR-54 | ✅ | `GET /api/v1/audit` |
| FR-55 | ✅ | `AddOpenTelemetrySystemMetrics()` |

### Ограничение нагрузки

| ID | Статус | Комментарий |
|---|---|---|
| FR-60 | ✅ | `AddRateLimiting()` + `UseRateLimiting()` |
| FR-61 | ✅ | `WikiTools:MaxResponseChars` |
| FR-62 | ✅ | Серверный потолок `top_k` |
| FR-63 | ✅ | `SearchTimeoutMs` через linked `CancellationTokenSource` |
| FR-64 | ✅ | Sliding-window two-bucket, Lua в Redis, партиция по ClientId или IP |
| FR-65 | ✅ | `[RateLimit]` / `WithMetadata<RateLimitAttribute>()` |
| FR-66 | ✅ | При ошибке Redis запрос пропускается |
| FR-67 | ✅ | 429 с `Retry-After` вместо отказа обслуживания |

## Нефункциональные требования

| ID | Статус | Комментарий |
|---|---|---|
| NFR-01 | ✅ | .NET 10, `LangVersion=14`, file-scoped namespaces |
| NFR-02 | ✅ | Minimal API |
| NFR-03 | ✅ | Официальный MCP .NET SDK |
| NFR-04 | ✅ | `internal sealed`, guard-паттерн, `TimeProvider`; 61 тест |
| NFR-05 | ✅ | `appsettings.json` + переменные окружения |
| NFR-06 | ✅ | Central Package Management |
| NFR-07 | ✅ | `TreatWarningsAsErrors`, `GenerateDocumentationFile` |
| NFR-08 | ✅ | Двухэтапный Dockerfile |
| NFR-09 | ✅ | Linux-контейнер |
| NFR-10 | ⚠️ | Пакеты Quality Gates заменены заглушками — Artifactory недоступен |
| NFR-11 | ⚠️ | То же: корпоративные пакеты подменены заглушками с теми же API |
| NFR-12 | ✅ | PostgreSQL 17 |
| NFR-13 | ⚠️ | В `NuGet.config` оставлен `nuget.org`; переключение описано в README |
| NFR-20 | ✅ | Состояние в PostgreSQL и Redis, не в процессе |
| NFR-21 | ✅ | `replicas: 2` в манифесте |
| NFR-22 | ✅ | Health-эндпоинты + пробы k8s |
| NFR-23 | ✅ | `terminationGracePeriodSeconds`, отмена по `stoppingToken` |
| NFR-24 | ⚠️ | Пакет resilience подключён; политики нужны при внешнем источнике |
| NFR-25 | ⚠️ | То же — для файлового источника circuit breaker не нужен |
| NFR-30 | ✅ | `RequireHttpsMetadata` вне Development; TLS терминируется ingress |
| NFR-31 | ✅ | Анонимный доступ к контенту закрыт |
| NFR-32 | ✅ | FluentValidation + guard-проверки |
| NFR-33 | — | Внешних URL сервер не запрашивает |
| NFR-34 | ✅ | Проверка вхождения в корень, симлинки не раскрываются |
| NFR-35 | ✅ | Секретов нет ни в коде, ни в `appsettings.json` |
| NFR-36 | ✅ | Контейнер от непривилегированного пользователя |
| NFR-37 | ✅ | Detailed errors только в Development |
| NFR-38 | ✅ | Маскирование включено по умолчанию |
| NFR-40 | ✅ | Serilog со структурированным выводом |
| NFR-41 | ✅ | Сегментный контекст, идентификаторы возвращаются в заголовках |
| NFR-42 | ✅ | OpenTelemetry-метрики ASP.NET Core и рантайма |
| NFR-43 | ✅ | Трассировка с OTLP-экспортом |

## Не реализовано (Could)

| Требование | Причина |
|---|---|
| `wiki_ask` | Требует LLM-провайдера, не определённого в BRD |
| `wiki_get_section` | Уровень Could; частично закрывается `offset` в `wiki_get_page` |
| `wiki_recent_changes` | Уровень Could; данные для него уже есть (`UpdatedAt`) |
| FR-15 semantic/vector search | Требует embedding-провайдера, не определённого в BRD |

## Открытые вопросы к владельцам системы

1. Раздел 5 BRD оставляет KPI параметрическими (`N`, `X`, «не менее»). Конкретные
   значения нужно согласовать — от них зависят настройки лимитов и SLA.
2. Не выбран промышленный источник wiki: реализован файловый, для Git и Confluence
   нужны отдельные реализации `IWikiSource`.
3. Конфигурация полнотекстового поиска зафиксирована как `russian`
   (`WikiDbContext.TextSearchConfiguration`). Для англоязычной wiki её нужно менять.
4. Исходный `spec.md` содержит повреждённые при копировании строки
   (NFR-03, NFR-05, NFR-07, NFR-12, NFR-22, NFR-38, NFR-41, NFR-42 обрезаны,
   BR-09 и BR-10 склеены). Требования восстановлены по контексту — стоит сверить
   с полной версией документа.
