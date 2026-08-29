# BRD: Центральный MCP-сервер для LLM Wiki на .NET

| Поле | Значение |
|---|---|
| Название проекта | Central Wiki MCP Server |
| Версия документа | v0.2 (скорректировано по ABDT best practices) |
| Дата | 11.08.2026 |
| Статус | Черновик для согласования |
| Технологический стек | .NET, ASP.NET Core, MCP SDK / HTTP JSON-RPC |
| Владелец продукта | Указать |
| Технический владелец | Указать |
| Спонсор | Указать |

---

## 1. Краткое резюме

Необходимо реализовать централизованный MCP-сервер, который предоставляет агентам, тестовым harness-ам и другим LLM-клиентам доступ к корпоративной LLM wiki через стандартизированный интерфейс MCP.

Цель — не копировать wiki на каждый компьютер с harness, а предоставлять доступ к ней из единого сервиса:

```text
Harness / LLM client
        |
        | MCP request
        v
Central Wiki MCP Server
        |
        v
Wiki storage / search index
```

Сервис должен предоставлять инструменты поиска и получения страниц wiki, а также контролировать доступ, логировать запросы и возвращать только разрешённый контент.

---

## 2. Предпосылка и проблема

Сейчас LLM wiki может копироваться на каждый компьютер или окружение, где работает harness. Это создаёт следующие проблемы:

1. Дублирование данных.
   - Одна и та же wiki хранится на множестве машин.
   - Сложно контролировать актуальность.

2. Риск устаревших знаний.
   - Harness может использовать старую версию документации.
   - Обновления нужно распространять отдельно.

3. Сложность администрирования.
   - Нужно синхронизировать файлы на всех машинах.
   - Сложно отзывать доступ.

4. Безопасность.
   - Контент может попадать на локальные диски.
   - Сложнее учитывать, кто и к чему имеет доступ.

5. Избыточное потребление ресурсов.
   - Полная копия wiki может быть большой.
   - Не вся wiki нужна каждому harness-узлу.

---

## 3. Цель проекта

Реализовать централизованный сервис на .NET, который:

1. Предоставляет доступ к LLM wiki через MCP.
2. Устраняет необходимость копировать wiki на клиентские машины.
3. Обеспечивает поиск, чтение и, при необходимости, краткую выдачу по wiki.
4. Учитывает права доступа пользователей или сервисных аккаунтов.
5. Логирует обращения и позволяет аудировать использование wiki.
6. Масштабируется и может использоваться несколькими harness-окружениями.

---

## 4. Ожидаемая бизнес-ценность

| Ценность | Описание |
|---|---|
| Единый источник знаний | Все harness-клиенты используют актуальную wiki |
| Снижение операционных затрат | Не нужно копировать и обновлять wiki на каждой машине |
| Безопасность | Доступ контролируется централизованно |
| Аудит | Видно, кто, когда и какие страницы запрашивал |
| Масштабируемость | Один сервис может обслуживать много harness-клиентов |
| Гибкость | Можно заменить источник wiki без изменения клиентов |
| Готовность к RAG | Сервер можно позже расширить до semantic search / ask-сценариев |

---

## 5. Критерии успеха / KPI

| Метрика | Целевое значение |
|---|---|
| Отсутствие локальной копии wiki на harness-машинах | 100% для пилотной группы |
| Актуальность данных | Задержки синхронизации не более X минут |
| Доступность сервиса | Например, 99.5% в рабочее время |
| Скорость поиска | p95 < 500 мс для top_k = 10 |
| Скорость получения страницы | p95 < 300 мс |
| Число подключённых harness-клиентов | Не менее N в пилоте |
| Снижение инцидентов из-за устаревшей wiki | Отсутствие повторных инцидентов после внедрения |
| Уровень ошибок MCP-интеграции | < 1% запросов с ошибками 5xx |

Конкретные числа нужно согласовать с владельцами системы.

---

## 6. Границы проекта

### 6.1. Входит в scope

1. Разработка MCP-сервера на .NET.
2. Подключение к источнику wiki:
   - файловая папка / Markdown;
   - Git-репозиторий;
   - Confluence / SharePoint / internal API;
   - другой источник по результатам анализа.
3. Индексирование wiki:
   - полнотекстовый поиск;
   - при необходимости vector search.
4. Реализация MCP tools:
   - `wiki_search`;
   - `wiki_get_page`;
   - `wiki_list_pages`;
   - опционально `wiki_ask`.
5. Аутентификация и авторизация.
6. Логирование и аудит.
7. Ограничение частоты запросов.
8. Деплой в Docker / Kubernetes / Windows Service.
9. Клиентская конфигурация для harness.
10. Опциональный локальный stdio-to-remote proxy, если harness умеет только запускать локальные MCP-процессы.

---

### 6.2. Не входит в scope первой версии

Если не согласовано отдельно:

1. Редактирование wiki через MCP.
2. Полноценный чат-бот над wiki.
3. Автоматическая генерация wiki.
4. Миграция wiki из одной системы в другую.
5. Мультирегиональная активная репликация.
6. Оффлайн-режим на всех клиентах.
7. Тонкая персонализация выдачи по пользователям, если это не требуется в MVP.
8. Поддержка всех возможных источников wiki сразу.

---

## 7. Заинтересованные стороны

| Роль | Интерес |
|---|---|
| Harness / LLM agent | Получать актуальные знания из wiki |
| Разработчики harness | Простая интеграция через MCP |
| Владельцы wiki | Актуальность и контроль доступа |
| Security / InfoSec | Аутентификация, авторизация, аудит |
| DevOps / Platform | Деплой, мониторинг, доступность |
| QA | Тестируемость и наблюдаемость |
| Бизнес-заказчик | Снижение затрат и рисков |

---

## 8. Пользовательские роли

### 8.1. Harness Service Account

Сервисный аккаунт, от имени которого harness обращается к MCP-серверу.

Права:

- поиск по разрешённой wiki;
- чтение страниц;
- ограниченный доступ к административным функциям отсутствует.

### 8.2. Пользователь-разработчик

Может использовать harness локально.

Права:

- доступ к wiki согласно своим правам;
- возможность видеть источники ответов.

### 8.3. Администратор MCP-сервера

Права:

- настройка источника wiki;
- запуск переиндексации;
- просмотр состояния сервиса;
- управление политиками доступа.

### 8.4. Аудитор / Security

Права:

- просмотр логов доступа;
- просмотр политик;
- экспорт аудита.

---

## 9. Основные сценарии использования

### UC-1. Harness ищет информацию в wiki

1. Harness получает задачу.
2. LLM решает, что нужна информация из wiki.
3. Harness вызывает MCP tool `wiki_search`.
4. MCP-сервер ищет релевантные страницы.
5. Сервер возвращает список фрагментов и метаданные.
6. Harness использует результат для дальнейшей работы.

Результат: harness получил релевантные знания без локальной копии wiki.

---

### UC-2. Harness читает конкретную страницу

1. Harness знает путь или идентификатор страницы.
2. Вызывает `wiki_get_page`.
3. MCP-сервер проверяет права.
4. Возвращает содержимое страницы в Markdown или plain text.

Результат: harness получил актуальную страницу.

---

### UC-3. Обновление индекса wiki

1. В wiki появляются новые страницы.
2. Фоновый worker запускает синхронизацию.
3. Сервис обновляет поисковый индекс.
4. Новые страницы становятся доступны через MCP.

Результат: клиенты получают свежие данные.

---

### UC-4. Попытка доступа без прав

1. Пользователь или сервис запрашивает страницу.
2. MCP-сервер проверяет ACL.
3. Если прав нет, возвращает ошибку или пустой результат.
4. Событие фиксируется в аудите.

Результат: запрещённый контент не выдаётся.

---

### UC-5. Harness работает только через stdio

1. Harness не умеет подключаться к удалённому HTTP MCP-серверу.
2. На машине запускается лёгкий локальный proxy.
3. Proxy принимает stdio MCP-запросы от harness.
4. Пересылает их в центральный MCP-сервер.
5. Возвращает ответы обратно.

Результат: harness работает, но wiki локально не хранится.

---

## 10. Бизнес-требования

| ID | Требование | Приоритет |
|---|---|---|
| BR-01 | Wiki должна быть доступна через единый централизованный сервис | Must |
| BR-02 | Harness-машины не должны хранить полную копию wiki | Must |
| BR-03 | Клиенты должны получать актуальный контент | Must |
| BR-04 | Доступ к wiki должен быть защищён | Must |
| BR-05 | Все обращения должны логироваться | Must |
| BR-06 | Решение должно быть реализуемо на .NET | Must |
| BR-07 | Интерфейс должен быть совместим с MCP-клиентами harness | Must |
| BR-08 | Сервис должен поддерживать поиск по wiki | Must |
| BR-09 | Сервис должен поддерживать чтение конкретных страниц | Must |
| BR-10 | Должна быть возможность ограничивать доступ по пользователям/группам | Should |
| BR-11 | Сервис должен масштабироваться горизонтально | Should |
| BR-12 | Должна быть возможность подключить несколько источников wiki в будущем | Could |
| BR-13 | Должна быть возможность расширить сервис до RAG / semantic search | Could |

---

## 11. Функциональные требования

### 11.1. MCP-сервер

| ID | Требование | Приоритет |
|---|---|---|
| FR-01 | Сервер должен реализовывать MCP-интерфейс через `ModelContextProtocol.AspNetCore` | Must |
| FR-02 | Сервер должен поддерживать сетевой транспорт: HTTP / Streamable HTTP / SSE, в зависимости от поддержки harness | Must |
| FR-03 | Сервер должен отдавать список доступных tools | Must |
| FR-04 | Сервер должен валидировать входные параметры (FluentValidation + Guard) | Must |
| FR-05 | Сервер должен возвращать структурированные JSON-ответы | Must |
| FR-06 | Сервер должен корректно обрабатывать ошибки MCP | Must |
| FR-07 | Сервер должен поддерживать health checks через `Abdt.Infrastructure.Monitoring.HealthCheck` (`AddAutoHealthChecks()` + `UseAutoHealthChecks()`): `/health`, `/health/full`, `/health/zabbix` | Must |
| FR-08 | Сервер должен поддерживать readiness check (через `/health/full`) | Should |
| FR-09 | Сервер должен поддерживать API Versioning через `Asp.Versioning.Http` | Must |

---

### 11.2. Tools для работы с wiki

#### Обязательные tools

| Tool | Назначение | Приоритет |
|---|---|---|
| `wiki_search` | Поиск по wiki | Must |
| `wiki_get_page` | Получение страницы | Must |

#### Дополнительные tools

| Tool | Назначение | Приоритет |
|---|---|---|
| `wiki_list_pages` | Список страниц / разделов | Should |
| `wiki_get_section` | Получение конкретной секции | Could |
| `wiki_ask` | Ответ на вопрос с поиском источников | Could |
| `wiki_recent_changes` | Последние изменения | Could |

---

### 11.3. Требования к `wiki_search`

Функция:

```text
wiki_search(query, top_k, filters)
```

Входные параметры:

| Поле | Тип | Обязательное | Описание |
|---|---|---:|---|
| `query` | string | Да | Поисковый запрос |
| `top_k` | int | Нет | Сколько результатов вернуть, по умолчанию 5 |
| `filters.namespace` | string | Нет | Раздел wiki |
| `filters.tags` | string[] | Нет | Фильтр по тегам |
| `filters.updated_after` | datetime | Нет | Только обновлённые после даты |

Выход:

```json
{
  "results": [
    {
      "pageId": "123",
      "path": "runbooks/deploy",
      "title": "Deployment runbook",
      "snippet": "How to deploy using harness...",
      "score": 0.87,
      "updatedAt": "2026-08-01T10:15:00Z",
      "revision": "abc123"
    }
  ]
}
```

Требования:

| ID | Требование | Приоритет |
|---|---|---|
| FR-10 | Поиск должен возвращать релевантные результаты | Must |
| FR-11 | Поиск должен учитывать права доступа | Must |
| FR-12 | Максимальный `top_k` должен ограничиваться сервером | Must |
| FR-13 | Результат должен включать источник и метаданные | Must |
| FR-14 | Поиск должен поддерживать полнотекстовый режим | Must |
| FR-15 | Поиск может поддерживать semantic / vector режим | Could |

---

### 11.4. Требования к `wiki_get_page`

Функция:

```text
wiki_get_page(pageId или path)
```

Выход:

```json
{
  "pageId": "123",
  "path": "runbooks/deploy",
  "title": "Deployment runbook",
  "contentMarkdown": "# Deploy\n...",
  "updatedAt": "2026-08-01T10:15:00Z",
  "revision": "abc123",
  "source": "wiki"
}
```

Требования:

| ID | Требование | Приоритет |
|---|---|---|
| FR-20 | Сервер должен возвращать страницу по id или path | Must |
| FR-21 | Сервер должен проверять права доступа | Must |
| FR-22 | Контент должен возвращаться в Markdown или plain text | Must |
| FR-23 | Сервер должен возвращать версию/ревизию страницы | Should |
| FR-24 | Сервер должен поддерживать частичную выдачу, если страница большая | Should |
| FR-25 | Сервер не должен возвращать бинарные файлы без явной поддержки | Should |

---

### 11.5. Синхронизация и индексация

| ID | Требование | Приоритет |
|---|---|---|
| FR-30 | Сервер должен уметь импортировать wiki из выбранного источника | Must |
| FR-31 | Должен существовать фоновый процесс синхронизации | Must |
| FR-32 | Синхронизация должна запускаться по расписанию | Must |
| FR-33 | Синхронизация должна запускаться вручную администратором | Should |
| FR-34 | Сервер должен отслеживать статус последней синхронизации | Must |
| FR-35 | Ошибки синхронизации должны быть видимы в логах и health/status | Must |
| FR-36 | Индекс должен обновляться инкрементально, если источник это поддерживает | Should |
| FR-37 | Должна быть возможность полного переиндексирования | Must |

---

### 11.6. Аутентификация и авторизация

| ID | Требование | Приоритет |
|---|---|---|
| FR-40 | Сервер должен требовать аутентификацию для всех MCP-запросов | Must |
| FR-41 | Поддержка JWT / OAuth2 / OIDC (Entra ID / Keycloak) | Should |
| FR-42 | Поддержка API-key для service-to-service сценариев | Should |
| FR-43 | Сервер должен различать пользователей и сервисные аккаунты | Must |
| FR-44 | Сервер должен применять политики доступа к страницам (namespace-based ACL) | Must |
| FR-45 | При отсутствии прав возвращать `401` ( unauthorized) или `403` (forbidden) через `TypedResults` | Must |
| FR-46 | Секреты не должны храниться в коде. В dev — `Abdt.Infrastructure.Configuration.Vault`, в prod — Kubernetes Secrets / env vars | Must |
| FR-47 | Валидация конфигурации через `Abdt.Infrastructure.Configuration.Validation` (`WithAbdtValidation()`) — ранний fail при ошибках | Must |

---

### 11.7. Логирование и аудит

> Логирование через `Abdt.Infrastructure.Logging.AspNetCore` (`AddRequestLogging()` + `AddAbdtLogger()`). Маскирование ПД по умолчанию. Сегментный контекст: `BsnOperationId`, `RequestId`, `Origin`.

| ID | Требование | Приоритет |
|---|---|---|
| FR-50 | Все MCP-вызовы должны логироваться через `Abdt.Infrastructure.Logging` | Must |
| FR-51 | Логи должны содержать timestamp, subject, tool, parameters hash/safe params, status. Сквозной correlation через `BsnOperationId`/`RequestId` | Must |
| FR-52 | Логи не должны содержать чувствительные данные. Маскирование через `DefaultPrivateProperties` (token, authorization, password, и др.) | Must |
| FR-53 | Должен быть аудит доступа к страницам (отдельная AuditEvent-таблица / log sink) | Must |
| FR-54 | Должна быть возможность экспорта логов во внешнюю систему | Should |
| FR-55 | Должны быть метрики запросов через `Abdt.Infrastructure.OpenTelemetry.Metrics` | Must |

---

### 11.8. Ограничение нагрузки

> Rate limiting реализуется через `Abdt.Infrastructure.RateLimiting` (двухуровневый: general DDoS-защита + business-лимиты, счётчики в Redis через Lua-скрипты, sliding-window two-bucket, stateless, fail-open).

| ID | Требование | Приоритет |
|---|---|---|
| FR-60 | Сервер должен поддерживать rate limiting через `Abdt.Infrastructure.RateLimiting` (`AddRateLimiting()` + `UseRateLimiting()`) | Must |
| FR-61 | Должен быть лимит на максимальный размер ответа (`ResponseLimits.MaxResponseChars`) | Must |
| FR-62 | Должен быть лимит на `top_k` (server-side, `Math.Min(topK, MaxTopK)`) | Must |
| FR-63 | Должен быть таймаут на поисковые запросы (`Search.TimeoutMs`) | Must |
| FR-64 | General-лимиты (DDoS-защита): partitioning по IP или ClientId, sliding-window, Redis | Must |
| FR-65 | Business-лимиты per-endpoint: через `[RateLimit]` attribute или `WithMetadata<RateLimitAttribute>()` | Should |
| FR-66 | Fail-open: при ошибке Redis запрос пропускается (не блокируется) | Must |
| FR-67 | Сервер должен деградировать gracefully при перегрузке | Should |

---

## 12. Нефункциональные требования

### 12.1. Технологические требования

| ID | Требование |
|---|---|
| NFR-01 | Реализация на .NET 10 LTS (предпочтительно), при ограничениях инфраструктуры — .NET 8 LTS. `LangVersion=14`, file-scoped namespaces |
| NFR-02 | Использование ASP.NET Core Minimal API для HTTP endpoint |
| NFR-03 | Использование официального MCP .NET SDK (`ModelContextProtocol` + `ModelContextProtocol.AspNetCore`). При недоступности — собственная реализация MCP JSON-RPC за abstraction-слоем |
| NFR-04 | Код должен быть модульным и пригодным для unit-тестирования. `internal sealed` классы, Guard-паттерн, `TimeProvider` для тестируемости времени |
| NFR-05 | Конфигурация через `appsettings.json`, environment variables. Секреты — через `Abdt.Infrastructure.Configuration.Vault` (dev) / Kubernetes Secrets (prod). Валидация конфигурации — `Abdt.Infrastructure.Configuration.Validation` (`WithAbdtValidation()`) |
| NFR-06 | Central Package Management (CPM): все NuGet-версии в `Directory.Packages.props`. Версии **не дублировать** в `.csproj` |
| NFR-07 | `Directory.Build.props`: `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`, `EnforceCodeStyleInBuild=true`, `EnableNETAnalyzers=true`. XML-комментарии обязательны |
| NFR-08 | Приложение должно собираться как container image (двухэтапный Docker-билд) |
| NFR-09 | Поддержка Linux-контейнеров; при необходимости Windows-хостинг |
| NFR-10 | **Quality Gates (обязательные пакеты)**: `Protoobp.Trace.Bundle`, `Abdt.Infrastructure.OpenTelemetry.Metrics`, `Abdt.Infrastructure.CodeAnalysis.Configuration`. Не удалять — обязательны для CI/CD |
| NFR-11 | Использовать корпоративные пакеты `Abdt.*` вместо сторонних аналогов (логирование, мониторинг, rate limiting, кэш, конфигурация) |
| NFR-12 | База данных — PostgreSQL (единственная реляционная БД ABDT). `NpgsqlDataSource` (Singleton) для pooling. NodaTime для temporal types. EF Core + `UseNodaTime()` |
| NFR-13 | NuGet-источники — только внутренний Artifactory (`artifactory.akbars.tech`). `nuget.org` удаляется из билда |

---

### 12.2. Производительность

| Параметр | Цель |
|---|---|
| Search p95 | < 500 ms для top_k = 10 |
| Get page p95 | < 300 ms |
| Concurrent clients | Не менее N одновременно |
| Throughput | Не менее X RPS |
| Max response size | Например, 100 KB или 2000 tokens |
| Index freshness | Не старше X минут |

---

### 12.3. Доступность и отказоустойчивость

| ID | Требование |
|---|---|
| NFR-20 | Сервис должен быть stateless там, где возможно |
| NFR-21 | Поддержка горизонтального масштабирования |
| NFR-22 | Health endpoints через `Abdt.Infrastructure.Monitoring`: `/health`, `/health/full`, `/health/zabbix` (авто-генерация по `IConfiguration`) |
| NFR-23 | Graceful shutdown |
| NFR-24 | Повторные попытки для внешних wiki-источников с retry/backoff (Polly) |
| NFR-25 | Circuit breaker для нестабильных внешних API (Polly) |

---

### 12.4. Безопасность

| ID | Требование |
|---|---|
| NFR-30 | TLS 1.2+ обязательно |
| NFR-31 | Запрет анонимного доступа к wiki-контенту |
| NFR-32 | Валидация всех входных параметров (FluentValidation + Guard) |
| NFR-33 | Защита от SSRF, если сервер ходит во внешние URL |
| NFR-34 | Защита от path traversal при доступе к файловым источникам |
| NFR-35 | Secrets только через `Abdt.Infrastructure.Configuration.Vault` (dev) / env vars / Kubernetes Secrets (prod). Не в коде, не в `appsettings.json` |
| NFR-36 | Минимальные привилегии service account |
| NFR-37 | Отключение debug endpoints в production |
| NFR-38 | Маскирование ПД в логах через `Abdt.Infrastructure.Logging` (`DefaultPrivateProperties`: token, authorization, password, и др.) |

---

### 12.5. Наблюдаемость

| ID | Требование |
|---|---|
| NFR-40 | Structured logging через `Abdt.Infrastructure.Logging` (`AddRequestLogging()` + `AddAbdtLogger()`). Маскирование ПД по умолчанию |
| NFR-41 | Correlation ID для запросов (через сегментный контекст `Abdt.Infrastructure.Logging`: `BsnOperationId`, `RequestId`) |
| NFR-42 | Метрики через `Abdt.Infrastructure.OpenTelemetry.Metrics` (`AddOpenTelemetrySystemMetrics()`). Prometheus-экспорт через `Abdt.Infrastructure.Monitoring.Prometheus` |
| NFR-43 | Tracing через `Protoobp.Trace.Bundle` (корпоративный OpenTelemetry, обязателен для Quality Gates) |
| NFR-44 | Экспорт в OpenTelemetry / Prometheus / Grafana при наличии инфраструктуры |
| NFR-45 | Алерты на 5xx, sync failure, high latency |

---

## 13. Целевая архитектура

### 13.1. Высокоуровневая схема

```text
+----------------------+        HTTPS / MCP         +---------------------------+
| Harness / LLM client | -------------------------> | Wiki MCP Server (.NET)    |
+----------------------+                            +---------------------------+
                                                             |
                                            +----------------+----------------+
                                            |                                 |
                                            v                                 v
                                   +------------------+              +------------------+
                                   | Search index     |              | Wiki source      |
                                   | FTS / vector DB  |              | Files/Git/API    |
                                   +------------------+              +------------------+

                                            ^
                                            |
                                   +------------------+
                                   | Sync worker      |
                                   +------------------+
```

---

### 13.2. Компоненты

#### 1. Wiki MCP API

ASP.NET Core приложение, которое:

- принимает MCP-запросы;
- проверяет аутентификацию;
- вызывает application-сервисы;
- возвращает результаты;
- пишет логи и метрики.

#### 2. Application layer (Vertical Slice)

Каждая фича — самодостаточная вертикаль (Endpoint + Handle + Request DTO + Validators + Mapping):

- `Features/Search/` — wiki_search (Endpoint + Handle + SearchRequest + Validator);
- `Features/GetPage/` — wiki_get_page;
- `Features/ListPages/` — wiki_list_pages;
- `Features/CheckAccess/` — проверка ACL;
- `Features/Audit/` — аудит доступа.

Endpoint обнаруживается автоматически через Scrutor (`EndpointBase` + сканирование сборок). Handle — статический метод с DI через параметры (не MediatR).

#### 3. Search provider

Абстракция над поиском (`WikiMcp.Search`):

- PostgreSQL FTS (MVP);
- Meilisearch;
- Elasticsearch/OpenSearch;
- Qdrant/pgvector для semantic search (Phase 3).

DI-регистрация через `Entry.cs` (`AddSearch()`).

#### 4. Wiki connector

Абстракция над источником wiki (`WikiMcp.Connectors.*`):

- FileSystem connector;
- Git connector;
- Confluence connector;
- SharePoint connector;
- Custom API connector.

DI-регистрация через `Entry.cs` (`AddConnectors()`).

#### 5. Sync worker

Фоновый процесс (`WikiMcp.Worker`, `BackgroundService`):

- забирает изменения из wiki;
- парсит страницы;
- строит chunks;
- обновляет индекс;
- пишет статус синхронизации.

DI-регистрация через `Entry.cs`. В интеграционных тестах `IHostedService` удаляются через `WebApplicationFactory`.

#### 6. Stdio proxy, опционально

Небольшое консольное .NET-приложение:

```text
harness stdio -> proxy -> remote MCP HTTP server
```

Нужно, если harness не умеет напрямую подключаться к удалённому MCP-серверу.

---

## 14. Рекомендуемая структура .NET solution

> Структура выровнена по ABDT-стандартам: CPM (`Directory.Packages.props`), `Directory.Build.props` с общими свойствами, Vertical Slice для features, Entry-паттерн для DI-регистрации по подслоям. См. `llm-wiki/topics/architecture.md`, `llm-wiki/concepts/central-package-management.md`.

```text
WikiMcp.sln
│
├── Directory.Build.props          # Общие свойства: TargetFramework, LangVersion, TreatWarningsAsErrors
├── Directory.Packages.props       # CPM — все NuGet-версии централизованно
├── global.json                    # dotnet SDK version
│
├── src
│   ├── WikiMcp.Server
│   │   ├── Program.cs             # Composition root — цепочка Entry-вызовов
│   │   ├── Endpoints/             # EndpointBase + Scrutor discovery
│   │   ├── Mcp/                   # MCP tools (McpServerToolType)
│   │   ├── Features/              # Vertical Slice: каждая фича — самодостаточная вертикаль
│   │   │   ├── Search/            #   wiki_search
│   │   │   │   ├── Search.cs      #   Endpoint + Handle
│   │   │   │   ├── Search.Request.cs
│   │   │   │   ├── Search.Validators.cs
│   │   │   │   └── Search.Mapping.cs
│   │   │   └── GetPage/           #   wiki_get_page
│   │   │       ├── GetPage.cs
│   │   │       ├── GetPage.Request.cs
│   │   │       └── GetPage.Validators.cs
│   │   └── Configuration/
│   │
│   ├── WikiMcp.Domain
│   │   ├── Entities/              # WikiPage, WikiChunk, SyncJob, AuditEvent
│   │   ├── ValueObjects/          # PagePath, Revision, ContentHash
│   │   └── Policies/              # Access policies, namespace rules
│   │
│   ├── WikiMcp.Infrastructure
│   │   ├── Persistence/           # DbContext, EntityTypeConfiguration, миграции
│   │   ├── Caching/               # Abdt.Infrastructure.Caching.Redis обёртка
│   │   ├── Logging/               # Abdt.Infrastructure.Logging конфигурация
│   │   ├── Configuration/         # Vault, Validation
│   │   └── Entry.cs               # DI-регистрация Infrastructure-слоя
│   │
│   ├── WikiMcp.Search
│   │   ├── Indexing/
│   │   ├── Querying/
│   │   ├── Providers/             # PostgreSQL FTS, Meilisearch, Qdrant
│   │   └── Entry.cs
│   │
│   ├── WikiMcp.Connectors.Abstractions
│   │   └── Entry.cs
│   │
│   ├── WikiMcp.Connectors.FileSystem
│   ├── WikiMcp.Connectors.Git
│   ├── WikiMcp.Connectors.Confluence
│   │
│   ├── WikiMcp.Worker
│   │   ├── SyncService.cs         # BackgroundService для синхронизации
│   │   └── Entry.cs
│   │
│   └── WikiMcp.Proxy
│       └── Program.cs             # stdio-to-remote proxy
│
├── tests
│   ├── WikiMcp.UnitTests          # xUnit v3 + MTP, Moq, AutoFixture, FluentAssertions
│   ├── WikiMcp.IntegrationTests   # WebApplicationFactory + Testcontainers (PostgreSQL, Redis)
│   └── WikiMcp.ContractTests      # MCP protocol contract tests
│
└── deploy
    ├── docker/
    │   └── Dockerfile             # Двухэтапный билд, ProtoOBP log dir
    ├── helm/
    └── appsettings/
        ├── appsettings.Development.json
        ├── appsettings.IntegrationTests.json
        └── appsettings.Production.json
```

### 14.1. Directory.Build.props (стандарт ABDT)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

### 14.2. Directory.Packages.props (CPM)

Все NuGet-версии определяются централизованно. Версии пакетов **не указываются** в отдельных `.csproj`.

```xml
<Project>
  <ItemGroup>
    <!-- MCP SDK -->
    <PackageVersion Include="ModelContextProtocol" Version="..." />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="..." />
    <!-- Quality Gates (обязательные) -->
    <PackageVersion Include="Protoobp.Trace.Bundle" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.OpenTelemetry.Metrics" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.CodeAnalysis.Configuration" Version="..." />
    <!-- ABDT Infrastructure -->
    <PackageVersion Include="Abdt.Infrastructure.Logging.AspNetCore" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.Monitoring.HealthCheck" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.RateLimiting" Version="6.0.0" />
    <PackageVersion Include="Abdt.Infrastructure.Caching.Redis" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.Configuration.Vault" Version="..." />
    <PackageVersion Include="Abdt.Infrastructure.Configuration.Validation" Version="..." />
    <!-- Database -->
    <PackageVersion Include="Npgsql" Version="..." />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="..." />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime" Version="..." />
    <!-- API -->
    <PackageVersion Include="FluentValidation" Version="..." />
    <PackageVersion Include="Asp.Versioning.Http" Version="..." />
    <PackageVersion Include="Asp.Versioning.Mvc.ApiExplorer" Version="..." />
    <PackageVersion Include="Asp.Versioning.OpenApi" Version="..." />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="..." />
    <PackageVersion Include="Scrutor" Version="..." />
    <PackageVersion Include="CommunityToolkit.Diagnostics" Version="..." />
    <!-- Resilience -->
    <PackageVersion Include="Polly" Version="..." />
    <PackageVersion Include="Microsoft.Extensions.Http.Polly" Version="..." />
    <!-- Tests -->
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="Microsoft.Testing.Platform" Version="2.2.3" />
    <PackageVersion Include="FluentAssertions" Version="7.1.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="AutoFixture" Version="4.18.1" />
    <PackageVersion Include="AutoFixture.AutoMoq" Version="4.18.1" />
    <PackageVersion Include="AutoFixture.Xunit3" Version="4.19.0" />
    <PackageVersion Include="Bogus" Version="35.6.5" />
    <PackageVersion Include="Testcontainers" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.13.0" />
  </ItemGroup>
</Project>
```

### 14.3. Entry-паттерн (DI-регистрация)

Каждый подслой имеет `Entry.cs` — статический класс с extension-методом для `IServiceCollection` / `IApplicationBuilder`. Composition root (`Program.cs`) собирает всё через цепочку вызовов:

```csharp
// Program.cs (composition root)
builder.Services
    .AddDomain()
    .AddInfrastructure(builder.Configuration)
    .AddSearch(builder.Configuration)
    .AddConnectors(builder.Configuration)
    .AddWikiMcpServer();

builder.Configuration.WithAbdtValidation();

var app = builder.Build();
app.UseAbdtLogging()
   .UseAutoHealthChecks("wiki-mcp")
   .UseRateLimiting()
   .MapWikiMcpEndpoints()
   .MapMcp("/mcp");
```

### 14.4. Vertical Slice для features

Каждая MCP-фича (search, get-page) — самодостаточная вертикаль:
- `Feature.cs` — Endpoint (наследник `EndpointBase`) + статический `Handle`-метод
- `Feature.Request.cs` — `internal sealed record` DTO
- `Feature.Validators.cs` — `internal sealed` FluentValidation-валидатор
- `Feature.Mapping.cs` — маппинг (при необходимости)
- Endpoint обнаруживается автоматически через Scrutor

---

## 15. Рекомендуемый технологический стек

> Стек выровнен по ABDT-стандартам. Корпоративные пакеты `Abdt.*` обязательны для прохождения Quality Gates CI/CD. Источник стандартов: `llm-wiki/topics/abdt.md`, `llm-wiki/entities/abdt-packages.md`.

### 15.1. Runtime и язык

| Область | Технология | Обоснование |
|---|---|---|
| Runtime | .NET 10 LTS (предпочтительно), .NET 8 LTS (fallback по инфраструктуре) | NFR-01, ABDT-стандарт |
| Language | C# 14 (`LangVersion=14`), file-scoped namespaces, primary constructors | `Directory.Build.props` ABDT ServiceTemplate |
| Web | ASP.NET Core Minimal API | Стандарт ABDT |
| MCP | `ModelContextProtocol` (официальный C# SDK) + `ModelContextProtocol.AspNetCore` для HTTP-транспорта | NFR-03 |
| Package Management | Central Package Management (CPM) через `Directory.Packages.props` | ABDT-стандарт, версии централизованно |

### 15.2. Quality Gates (обязательные пакеты)

> Эти пакеты **нельзя удалять** — они обязательны для CI/CD пайплайна ABDT.

| Пакет | Назначение |
|---|---|
| `Protoobp.Trace.Bundle` | OpenTelemetry трассировка (корпоративный) |
| `Abdt.Infrastructure.OpenTelemetry.Metrics` | Системные метрики через OpenTelemetry |
| `Abdt.Infrastructure.CodeAnalysis.Configuration` | Roslyn-анализатор appsettings.json (статический анализ) |

### 15.3. Корпоративные инфраструктурные пакеты Abdt.*

> Использовать корпоративные пакеты вместо сторонних аналогов. Источник: `gitlab.akbars.tech/sharedprojects/`.

| Область | Пакет | Точка входа | Заменяет |
|---|---|---|---|
| Логирование | `Abdt.Infrastructure.Logging.AspNetCore` | `services.AddRequestLogging(...)` + `builder.AddAbdtLogger()` | Serilog, ручное логирование HTTP |
| Логирование HTTP (исходящие) | `Abdt.Infrastructure.Logging.AspNetCore.HttpExtensions` | `httpClientBuilder.AddRequestLogging(...)` | Ручные DelegatingHandler |
| Мониторинг (health checks) | `Abdt.Infrastructure.Monitoring.HealthCheck` | `services.AddAutoHealthChecks(...)` + `app.UseAutoHealthChecks()` | `AddHealthChecks()`, ручные `/healthz` |
| Мониторинг (Prometheus) | `Abdt.Infrastructure.Monitoring.Prometheus` | метрики Prometheus | Сторонние Prometheus-интеграции |
| Rate Limiting | `Abdt.Infrastructure.RateLimiting` | `services.AddRateLimiting(...)` + `app.UseRateLimiting()` | `System.Threading.RateLimiting` (встроенный) |
| Кэш | `Abdt.Infrastructure.Caching.Redis` | `services.AddRedisCache(...)` | Сторонние Redis-клиенты |
| Конфигурация (Vault) | `Abdt.Infrastructure.Configuration.Vault` | `builder.Configuration.AddLocalDevelopmentVault(...)` | Ручное управление секретами в dev |
| Конфигурация (валидация) | `Abdt.Infrastructure.Configuration.Validation` | `builder.Configuration.WithAbdtValidation(...)` | Ручная валидация конфигурации |
| OpenTelemetry (метрики) | `Abdt.Infrastructure.OpenTelemetry.Metrics` | `services.AddOpenTelemetrySystemMetrics(...)` | Сторонние OTel-интеграции |

### 15.4. База данных и поиск

| Область | Технология | Обоснование |
|---|---|---|
| Database | PostgreSQL (единственная реляционная БД ABDT) | ABDT-стандарт |
| ORM | EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` | Не Dapper — ABDT-стандарт EF Core |
| Temporal types | NodaTime + `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` | `Instant` вместо `DateTime`, ABDT-стандарт |
| Connection pooling | `NpgsqlDataSource` (Singleton) | ABDT-паттерн, общий пул для всех DbContext |
| Search | PostgreSQL FTS (MVP) / Meilisearch / OpenSearch / Qdrant (Phase 3) | Поэтапное расширение |
| Cache | `Abdt.Infrastructure.Caching.Redis` (Redis) | ABDT-стандарт |

### 15.5. API и валидация

| Область | Технология | Обоснование |
|---|---|---|
| Validation | FluentValidation | ABDT-стандарт, endpoint filters |
| API Versioning | `Asp.Versioning.Http` + `Asp.Versioning.Mvc.ApiExplorer` + `Asp.Versioning.OpenApi` | ABDT-стандарт |
| OpenApi | `Microsoft.AspNetCore.OpenApi` (НЕ Swashbuckle/SwaggerGen) | ABDT-стандарт |
| Endpoint discovery | Scrutor (сканирование сборок, `EndpointBase`) | ABDT-паттерн |
| HTTP responses | `TypedResults` (строго типизированные) | ABDT-стандарт, OpenApi inference |
| Guard | `CommunityToolkit.Diagnostics` (`Guard.IsNotNull`, `Guard.IsGreaterThan`) | ABDT-стандарт, early return |

### 15.6. Resilience и HTTP

| Область | Технология |
|---|---|
| HTTP clients | `HttpClientFactory` + `Polly` (retry, timeout, circuit breaker) |
| Resilience | Polly: retry, timeout, circuit breaker — для внешних wiki-источников |

### 15.7. Тестирование

| Область | Технология | Версия / ограничение |
|---|---|---|
| Framework | xUnit v3 | `xunit.v3` 3.2.2 |
| Runner | Microsoft.Testing.Platform (MTP) | 2.2.3 (НЕ VSTest) |
| Assertions | FluentAssertions | **<=7.x** (8.0+ — платная лицензия!) |
| Mocking | Moq | 4.20.72 |
| Test data | AutoFixture + AutoFixture.AutoMoq + AutoFixture.Xunit3 | 4.18.1 / 4.19.0 |
| Fake data | Bogus | 35.6.5 |
| Integration | WebApplicationFactory | `Microsoft.AspNetCore.Mvc.Testing` |
| Containers | Testcontainers | 4.13.0 (PostgreSQL, Redis) |

### 15.8. Деплой и инфраструктура

| Область | Технология | Обоснование |
|---|---|---|
| Containers | Docker (двухэтапный билд) | ABDT-стандарт |
| Orchestration | Kubernetes / Docker Compose (dev/pilot) | ABDT CI/CD → K8s |
| CI/CD | GitLab CI (`cicd/gitlabci-templates`) | ABDT-стандарт, `dotNET_VERSION=10.0` |
| NuGet | Внутренний Artifactory (`artifactory.akbars.tech`), `nuget.org` удаляется | ABDT-стандарт |
| Docker registry | Корпоративный Artifactory | ABDT-стандарт |
| Secrets | `Abdt.Infrastructure.Configuration.Vault` (HashiCorp Vault, dev) / Kubernetes Secrets (prod) | ABDT-стандарт |
| Auth | JWT / API keys / Entra ID / Keycloak | По согласованию с Security |

### 15.9. Observability

| Область | Пакет | Назначение |
|---|---|---|
| Tracing | `Protoobp.Trace.Bundle` | Корпоративный OpenTelemetry трейсинг (обязателен) |
| Metrics | `Abdt.Infrastructure.OpenTelemetry.Metrics` | Системные метрики (обязателен) |
| Health checks | `Abdt.Infrastructure.Monitoring.HealthCheck` | Auto-health-checks по `IConfiguration` |
| Prometheus | `Abdt.Infrastructure.Monitoring.Prometheus` | Экспорт метрик |
| Logging | `Abdt.Infrastructure.Logging.AspNetCore` | Структурированное логирование с маскированием ПД |

---

## 16. Модель данных

> ABDT-стандарт: NodaTime для temporal types (`Instant` вместо `DateTime`). PostgreSQL через Npgsql + EF Core. `NpgsqlDataSource` (Singleton) для connection pooling. Свойства агрегатов — `{ get; internal set; }`, внутренние сущности — `{ get; init; }`.

### 16.1. WikiPage

| Поле | Тип (NodaTime) | Описание |
|---|---|---|
| Id | string / GUID | Уникальный идентификатор |
| Path | string | Путь или slug |
| Title | string | Заголовок |
| SourceUri | string | URI источника |
| Namespace | string | Раздел / space |
| Revision | string | Версия |
| UpdatedAt | `Instant` | Дата обновления (NodaTime, не DateTime) |
| ContentHash | string | Хэш контента |
| IsDeleted | bool | Признак удаления |

### 16.2. WikiChunk

| Поле | Тип | Описание |
|---|---|---|
| Id | GUID | Уникальный идентификатор |
| PageId | string | Ссылка на страницу |
| SectionTitle | string | Заголовок секции |
| Content | string | Текст чанка |
| Embedding | vector, optional | Вектор для semantic search |
| TokenCount | int | Оценка размера |
| Ordinal | int | Порядок в странице |

### 16.3. SyncJob

| Поле | Тип (NodaTime) | Описание |
|---|---|---|
| Id | GUID | Идентификатор |
| StartedAt | `Instant` | Начало (NodaTime) |
| FinishedAt | `Instant?` | Окончание (NodaTime, nullable) |
| Status | enum | Running / Success / Failed |
| PagesAdded | int | Добавлено |
| PagesUpdated | int | Обновлено |
| PagesDeleted | int | Удалено |
| ErrorMessage | string? | Ошибка (nullable) |

### 16.4. AuditEvent

| Поле | Тип (NodaTime) | Описание |
|---|---|---|
| Id | GUID | Идентификатор |
| Timestamp | `Instant` | Время (NodaTime, не DateTime) |
| Subject | string | Пользователь / service account |
| ToolName | string | Имя MCP tool |
| RequestHash | string | Хэш параметров |
| PageId | string? | Запрошенная страница, если есть (nullable) |
| ResultStatus | string | Success / Denied / Error |
| CorrelationId | string | Сквозной ID (`BsnOperationId` из `Abdt.Infrastructure.Logging`) |

> **EF Core конфигурация**: `UseNpgsql(dataSource, ob => ob.UseNodaTime())` — обязательная интеграция NodaTime. `NpgsqlDataSource` регистрируется как Singleton для общего пула соединений.

---

## 17. Интерфейс MCP

### 17.1. Пример tools

```json
{
  "tools": [
    {
      "name": "wiki_search",
      "description": "Search internal LLM wiki",
      "inputSchema": {
        "type": "object",
        "properties": {
          "query": { "type": "string" },
          "top_k": { "type": "integer", "default": 5 },
          "namespace": { "type": "string" }
        },
        "required": ["query"]
      }
    },
    {
      "name": "wiki_get_page",
      "description": "Get wiki page by path or id",
      "inputSchema": {
        "type": "object",
        "properties": {
          "page": { "type": "string" }
        },
        "required": ["page"]
      }
    }
  ]
}
```

---

### 17.2. Пример ответа `wiki_search`

```json
{
  "results": [
    {
      "pageId": "runbooks-deploy",
      "path": "runbooks/deploy",
      "title": "Deploy runbook",
      "snippet": "Deployment through harness requires...",
      "score": 0.91,
      "updatedAt": "2026-08-01T09:00:00Z",
      "revision": "12345"
    }
  ],
  "total": 1,
  "query": "deploy harness"
}
```

---

### 17.3. Пример ответа `wiki_get_page`

```json
{
  "pageId": "runbooks-deploy",
  "path": "runbooks/deploy",
  "title": "Deploy runbook",
  "contentMarkdown": "# Deploy\n\n...",
  "updatedAt": "2026-08-01T09:00:00Z",
  "revision": "12345",
  "source": "wiki"
}
```

---

### 17.4. Ошибки

Стандартизированные коды:

| Код | Причина |
|---|---|
| `unauthorized` | Нет аутентификации |
| `forbidden` | Нет прав |
| `page_not_found` | Страница не найдена |
| `invalid_argument` | Невалидные параметры |
| `rate_limited` | Превышен лимит запросов |
| `index_unavailable` | Индекс временно недоступен |
| `upstream_error` | Ошибка источника wiki |
| `internal_error` | Непредвиденная ошибка |

---

## 18. Требования к интеграции с harness

### 18.1. Если harness поддерживает удалённый MCP

Конфигурация клиента может выглядеть так:

```json
{
  "mcpServers": {
    "wiki": {
      "url": "https://wiki-mcp.corp.local/mcp",
      "headers": {
        "Authorization": "Bearer ${WIKI_MCP_TOKEN}"
      }
    }
  }
}
```

---

### 18.2. Если harness поддерживает только stdio

Используется локальный proxy:

```json
{
  "mcpServers": {
    "wiki": {
      "command": "dotnet",
      "args": [
        "WikiMcp.Proxy.dll",
        "--url",
        "https://wiki-mcp.corp.local/mcp"
      ],
      "env": {
        "WIKI_MCP_TOKEN": "${WIKI_MCP_TOKEN}"
      }
    }
  }
}
```

Такой proxy:

- запускается как локальный процесс;
- общается с harness через stdio;
- проксирует запросы в центральный сервис;
- не хранит wiki локально.

---

## 19. Безопасность

### 19.1. Аутентификация

Рекомендуется:

1. Для пользователей:
   - OAuth2 / OIDC;
   - Entra ID / Keycloak;
   - JWT access token.

2. Для harness service accounts:
   - client credentials flow;
   - API key с ротацией;
   - managed identity, если инфраструктура позволяет.

### 19.2. Авторизация

Минимальная модель:

```text
Subject -> Role/Group -> Allowed namespaces/pages -> Read
```

Пример политик:

| Subject | Доступ |
|---|---|
| harness-ci | read: runbooks/*, docs/harness/* |
| developer | read: allowed spaces only |
| admin | read all + admin endpoints |
| auditor | read audit logs |

### 19.3. Защита контента

- Не возвращать страницы, к которым нет доступа (проверка ACL до возврата контента).
- Не кэшировать чувствительный контент в открытом виде без необходимости.
- Маскировать токены и секреты в логах через `Abdt.Infrastructure.Logging` (`DefaultPrivateProperties`: `token`, `authorization`, `secret`, `password`, `access_token`, `refresh_token`, и др.).
- Не включать содержимое страниц в логи по умолчанию.
- `MaxContentSize` — лимит размера контента для логирования (~300 KB).
- Валидация входных параметров через FluentValidation + Guard (`CommunityToolkit.Diagnostics`).

### 19.4. Секреты

- **Development**: `Abdt.Infrastructure.Configuration.Vault` (HashiCorp Vault, `AddLocalDevelopmentVault()`).
- **Production**: Kubernetes Secrets / environment variables.
- Секреты **не хранятся** в `appsettings.json` или коде.
- Валидация конфигурации: `Abdt.Infrastructure.Configuration.Validation` (`WithAbdtValidation()`) — ранний fail при ошибках (в non-dev окружениях).
- `Abdt.Infrastructure.CodeAnalysis.Configuration` — Roslyn-анализатор для обнаружения секретов в `appsettings.json` (Quality Gates).

### 19.5. Сетевые требования

- TLS обязательно (1.2+).
- Внутренний endpoint, если wiki внутренняя.
- Firewall / network policy.
- При необходимости IP allowlist.
- Rate limiting на gateway и application уровне (`Abdt.Infrastructure.RateLimiting` — двухуровневый, Redis-based).

---

## 20. Observability

> Observability реализуется через корпоративные ABDT-пакеты. `Protoobp.Trace.Bundle` и `Abdt.Infrastructure.OpenTelemetry.Metrics` обязательны для Quality Gates.

### 20.1. Логи (Abdt.Infrastructure.Logging)

Структурированное логирование через `Abdt.Infrastructure.Logging.AspNetCore`:
- `AddRequestLogging()` — перехват входящих HTTP-запросов с маскированием ПД
- `AddAbdtLogger()` — адаптер к `Microsoft.Extensions.Logging.ILogger`
- Маскирование по умолчанию: `cookie`, `authorization`, `token`, `secret`, `password`, `access_token`, `refresh_token`, `pan`, `cvc`, `card`, `phone`, `firstName/lastName/middleName`, `base64`, `body`, `pdf` и др.
- `MaxContentSize = 307200` (~300 KB) — лимит размера контента для логирования
- Сегментный контекст: `BsnOperationId`, `RequestId`, `Origin` — сквозная корреляция

Пример structured log:

```json
{
  "timestamp": "2026-08-10T12:00:00Z",
  "level": "Information",
  "bsnOperationId": "e5b2...",
  "requestId": "a1f3...",
  "subject": "harness-ci",
  "tool": "wiki_search",
  "queryLength": 24,
  "topK": 5,
  "durationMs": 180,
  "status": "success"
}
```

### 20.2. Метрики (Abdt.Infrastructure.OpenTelemetry.Metrics + Prometheus)

| Метрика | Описание |
|---|---|
| `wiki_mcp_requests_total` | Число запросов |
| `wiki_mcp_request_duration_seconds` | Длительность |
| `wiki_mcp_errors_total` | Число ошибок |
| `wiki_mcp_search_results_total` | Сколько результатов найдено |
| `wiki_mcp_cache_hits_total` | Попадания в кэш |
| `wiki_mcp_sync_last_success_timestamp` | Последняя успешная синхронизация |
| `wiki_mcp_sync_status` | Статус синхронизации |

Экспорт метрик:
- `Abdt.Infrastructure.OpenTelemetry.Metrics` — системные метрики (обязательно для Quality Gates)
- `Abdt.Infrastructure.Monitoring.Prometheus` — Prometheus-эндпоинт

### 20.3. Трассировка (Protoobp.Trace.Bundle)

Корпоративный OpenTelemetry-трейсинг — обязательный для ABDT CI/CD. Обеспечивает distributed tracing для MCP-запросов, sync worker и connector-вызовов.

### 20.4. Health checks (Abdt.Infrastructure.Monitoring.HealthCheck)

Авто-генерация health checks по `IConfiguration` через `AddAutoHealthChecks()`:

```text
GET /health         — базовая проверка
GET /health/full    — полная проверка всех ресурсов
GET /health/zabbix  — Zabbix-формат
```

`/status` (кастомный endpoint) может возвращать:

```json
{
  "service": "wiki-mcp",
  "version": "1.0.0",
  "index": "ready",
  "lastSync": {
    "status": "success",
    "finishedAt": "2026-08-10T11:45:00Z"
  }
}
```

> Health checks автоматически определяют ресурсы по `IConfiguration` (БД, Redis, HTTP-эндпоинты, IdentityProvider) и регистрируют соответствующие проверки. Проверки выполняются параллельно.

---

## 21. Деплой и эксплуатация

> Деплой выровнен по ABDT CI/CD стандартам: GitLab-шаблоны, корпоративный Artifactory, двухэтапный Docker-билд, ProtoOBP log directory.

### 21.1. Варианты деплоя

| Вариант | Когда подходит |
|---|---|
| Docker + Kubernetes | Облачная / контейнерная инфраструктура (рекомендуется ABDT) |
| Docker Compose | Небольшой стенд / dev / pilot |
| Windows Service | Если инфраструктура Windows-only |
| Systemd service | Linux VM без Kubernetes |

### 21.2. Environments

| Environment | Назначение |
|---|---|
| Development | Разработка (Vault для dev-секретов, валидация конфигурации отключена) |
| IntegrationTests | Интеграционное тестирование (Testcontainers, `IHostedService` удалены) |
| Staging | Предпродукционная проверка |
| Production | Продуктив (валидация конфигурации включена, `ThrowOnError=true`) |

### 21.3. Docker (двухэтапный билд, ABDT-стандарт)

```dockerfile
# Stage 1: builder
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# NuGet — только внутренний Artifactory, nuget.org удаляется
RUN dotnet nuget remove source nuget.org
# RUN dotnet nuget add source https://artifactory.akbars.tech/api/nuget/abdt-nuget --name artifactory

COPY . .
RUN dotnet restore WikiMcp.sln
RUN dotnet publish src/WikiMcp.Server -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# ProtoOBP log directory (обязательно для корпоративного трейсинга)
RUN mkdir -p /var/log/protoobp/dotnet && chmod a+rwx /var/log/protoobp/dotnet

COPY --from=builder /app/publish .
ENTRYPOINT ["dotnet", "WikiMcp.Server.dll"]
```

### 21.4. CI/CD (GitLab, ABDT-стандарт)

`.gitlab-ci.yml` подключает корпоративные шаблоны:

```yaml
include:
  - project: 'cicd/gitlabci-templates'
    ref: master
    file:
      - gitlabci/deploy_k8s.yaml

variables:
  dotNET_VERSION: '10.0'
  INTEGRATION_TESTS_ENABLED: 'true'
  SERVICE_NAME: 'wiki-mcp'
  SERVICE_POSTFIX: 'Web'
  LIBC_RUNTIME: 'linux-x64'
```

Стадии CI/CD:

1. **Build** — сборка решения (`dotnet build WikiMcp.sln`)
2. **Test** — unit и интеграционные тесты (`dotnet test`)
3. **Quality Gate** — статический анализ (`Protoobp.Trace.Bundle` + `Abdt.Infrastructure.CodeAnalysis.Configuration`)
4. **Deploy** — деплой в Kubernetes

### 21.5. Конфигурация

Пример `appsettings.Production.json`:

```json
{
  "Wiki": {
    "Source": "FileSystem",
    "FileSystem": {
      "RootPath": "/data/wiki"
    },
    "SyncIntervalMinutes": 15
  },
  "Search": {
    "Provider": "PostgresFts",
    "DefaultTopK": 5,
    "MaxTopK": 20,
    "TimeoutMs": 2000
  },
  "Auth": {
    "Mode": "JwtBearer",
    "Authority": "https://auth.corp.local/realms/wiki"
  },
  "App": {
    "RateLimiting": {
      "ClientAppUniqueName": "wiki-mcp",
      "RedisConnection": "redis-cluster:6379",
      "GeneralRules": [ { "Limit": 300, "Interval": "InMinute" } ],
      "GeneralPartitionBy": "ClientId",
      "GeneralMode": "All"
    }
  },
  "ResponseLimits": {
    "MaxResponseChars": 200000,
    "MaxSnippetChars": 1000
  }
}
```

> Rate limiting конфигурация в секции `App:RateLimiting` (ABDT-стандарт `Abdt.Infrastructure.RateLimiting`).

Секреты (через environment variables / Kubernetes Secrets / Vault):

```text
WIKI_MCP_DB_CONNECTION        # PostgreSQL connection string
WIKI_MCP_AUTH_KEY             # JWT signing key
WIKI_MCP_API_KEY              # API key for service-to-service
WIKI_MCP_REDIS_CONNECTION     # Redis connection for rate limiting + cache
```

> В `Development` секреты поставляются через `Abdt.Infrastructure.Configuration.Vault` (HashiCorp Vault, `AddLocalDevelopmentVault()`). В `Production` — через Kubernetes Secrets / environment variables. Секреты **не хранятся** в `appsettings.json` или коде.

---

## 22. Требования к тестированию

> Тестовый стек выровнен по ABDT ServiceTemplate: xUnit v3 + MTP, FluentAssertions (<=7.x), Moq, AutoFixture, Testcontainers.

### 22.0. Тестовый стек (ABDT ServiceTemplate)

| Пакет | Версия | Назначение |
|---|---|---|
| `xunit.v3` | 3.2.2 | Тест-фреймворк |
| `Microsoft.Testing.Platform` | 2.2.3 | Раннер MTP (НЕ VSTest) |
| `FluentAssertions` | **7.1.0** (не выше 7.x!) | Assertions (8.0+ — платная лицензия) |
| `Moq` | 4.20.72 | Мокирование |
| `AutoFixture` | 4.18.1 | Генерация тестовых данных |
| `AutoFixture.AutoMoq` | 4.18.1 | AutoMoq-кастомизация |
| `AutoFixture.Xunit3` | 4.19.0 | xUnit v3 интеграция |
| `Bogus` | 35.6.5 | Fake data generation |
| `Testcontainers` | 4.13.0 | Docker-контейнеры (PostgreSQL, Redis) |
| `Testcontainers.PostgreSql` | 4.13.0 | PostgreSQL container |
| `Testcontainers.Redis` | 4.13.0 | Redis container |

> **Важно:** xUnit v3 использует Microsoft.Testing.Platform (MTP), не VSTest. Синтаксис фильтров отличается: `--filter-class`, `--filter-method`, `--filter-trait`, `--filter-query`.

### 22.1. Unit tests

Проверить:

- валидация входных параметров (FluentValidation-валидаторы);
- логика ограничения `top_k` (Guard + server-side limit);
- применение политик доступа (ACL);
- форматирование ответов (DTO mapping);
- обработка ошибок (Guard, early returns);
- маппинг NodaTime types.

Паттерны:

- `BaseUnitTest` с AutoFixture + AutoMoq (`new Fixture().Customize(new AutoMoqCustomization())`)
- `Mock<ILogger>` — логгер всегда мокается
- В `Dispose` — проверка отсутствия логов ошибок (`Logger.Verify(...)`)
- AAA: Arrange, Act, Assert
- `Assert.Equal(expected, actual)` или FluentAssertions `result.Should().Be(...)`

### 22.2. Integration tests

Проверить:

- MCP endpoint отвечает;
- `wiki_search` возвращает результаты из тестового индекса;
- `wiki_get_page` возвращает страницу;
- unauthorized запрос получает 401;
- forbidden запрос получает 403;
- sync worker обновляет индекс;
- health checks (`/health`, `/health/full`) отвечают.

Паттерны:

```csharp
// WebApplicationFactory с удалением IHostedService
public class ApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly InfrastructureForTest _infrastructure = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        builder.UseEnvironment("IntegrationTests");
    }

    public async ValueTask InitializeAsync() => await _infrastructure.InitializeAsync();
}

// BaseApiTest
public class BaseApiTest : IAsyncDisposable
{
    protected readonly HttpClient ApiClient;
    protected readonly IServiceScope Scope;

    public BaseApiTest(ApplicationFactory factory)
    {
        ApiClient = factory.CreateClient();
        Scope = factory.Services.CreateScope();
    }
}
```

- Environment: `IntegrationTests`
- Все `IHostedService` удаляются в тестовой фабрике
- PostgreSQL и Redis — через Testcontainers
- Интеграционные тесты требуют Docker

### 22.3. Contract tests

Проверить совместимость с MCP-клиентом:

- `initialize`;
- `tools/list`;
- `tools/call`;
- error handling;
- transport compatibility (HTTP / Streamable HTTP / SSE).

### 22.4. Load tests

Проверить:

- p95 latency;
- поведение под нагрузкой;
- rate limiting (ABDT `Abdt.Infrastructure.RateLimiting` — двухуровневый, Redis-based);
- деградацию при недоступном источнике (fail-open для rate limiting);
- потребление памяти.

### 22.5. Security tests

Проверить:

- path traversal (особенно для FileSystem connector);
- unauthorized access (401);
- forbidden access (403);
- oversized requests (ResponseLimits);
- injection attacks (SQL injection в search, path traversal в get_page);
- secrets not in logs (маскирование через `Abdt.Infrastructure.Logging`);
- CORS/network policy.

---

## 23. План реализации

### Phase 0. Discovery и проектирование

Длительность: 1 неделя

Задачи:

1. Уточнить источник wiki.
2. Уточнить, какой транспорт поддерживает harness.
3. Определить auth provider.
4. Выбрать search backend.
5. Согласовать API tools.
6. Определить ACL.
7. Подготовить ADR по архитектуре.

Выход:

- утверждённый BRD;
- architecture decision record;
- backlog MVP.

---

### Phase 1. MVP

Длительность: 2–3 недели

Scope:

1. ASP.NET Core MCP server.
2. Подключение к одному источнику wiki.
3. Индексация страниц.
4. Tools:
   - `wiki_search`;
   - `wiki_get_page`.
5. Auth через token/JWT.
6. Логирование.
7. Health checks.
8. Docker image.
9. Пилотное подключение одного harness.

Выход:

- harness может искать и читать wiki;
- локальная копия wiki не требуется.

---

### Phase 2. Hardening

Длительность: 2 недели

Scope:

1. RBAC / namespace filtering.
2. Rate limiting.
3. Метрики и tracing.
4. Аудит.
5. Retry / timeout / circuit breaker.
6. Load testing.
7. Security review.
8. Runbooks.

Выход:

- сервис готов к расширенному использованию.

---

### Phase 3. Rollout и развитие

Длительность: по ситуации

Scope:

1. Подключение дополнительных команд/harness.
2. Подключение дополнительных источников.
3. Vector search.
4. `wiki_ask`.
5. Кэширование.
6. High availability.
7. Admin UI при необходимости.

---

## 24. Критерии приёмки

### 24.1. Функциональная приёмка

1. Harness может подключиться к MCP-серверу.
2. Harness получает список tools.
3. `wiki_search` возвращает релевантные результаты.
4. `wiki_get_page` возвращает содержимое страницы.
5. Запрос без токена возвращает `401`.
6. Запрос без прав возвращает `403`.
7. Несуществующая страница возвращает понятную ошибку.
8. Логи содержат факт обращения.
9. Wiki не хранится на harness-машине.
10. После обновления wiki изменения доступны в течение согласованного SLA.

---

### 24.2. Операционная приёмка

1. Сервис запускается в контейнере (двухэтапный Docker-билд, ProtoOBP log dir).
2. `/health` и `/health/full` возвращают OK (через `Abdt.Infrastructure.Monitoring`).
3. Метрики собираются (`Abdt.Infrastructure.OpenTelemetry.Metrics` + Prometheus).
4. Логи структурированы (`Abdt.Infrastructure.Logging`, маскирование ПД).
5. Ошибки синхронизации видны оператору (health check sync status).
6. Существует runbook для деплоя.
7. Существует runbook для переиндексации.
8. Secrets не хранятся в репозитории (Vault для dev, K8s Secrets для prod).

---

## 25. Риски и митигация

| Риск | Вероятность | Влияние | Митигация |
|---|---:|---:|---|
| Harness не поддерживает удалённый MCP | Средняя | Высокое | Сделать stdio proxy |
| MCP SDK для .NET незрелый | Средняя | Среднее | Использовать абстракцию, при необходимости реализовать JSON-RPC вручную |
| Источник wiki не имеет удобного API | Средняя | Высокое | Начать с файлового экспорта или Git-репозитория |
| Большие страницы перегружают LLM | Высокая | Среднее | Возвращать snippets, лимитировать размер |
| Права доступа сложные | Средняя | Высокое | Начать с namespace-based ACL, затем расширять |
| Задержки сети | Средняя | Среднее | Кэш, компактные ответы, p95 monitoring |
| Утечка чувствительных данных | Низкая | Высокое | AuthZ, audit, маскирование логов, security review |
| Индекс устаревает | Средняя | Среднее | Мониторинг last sync, алерты, ручной reindex |
| Высокая нагрузка от агентов | Средняя | Среднее | Rate limiting, quotas, timeout |

---

## 26. Открытые вопросы

Нужно уточнить до старта:

1. Какой источник wiki используется сейчас?
   - Markdown files?
   - Git?
   - Confluence?
   - SharePoint?
   - Custom DB?

2. Какой транспорт MCP поддерживает harness?
   - remote HTTP / SSE / Streamable HTTP;
   - только stdio;
   - нужна проверка.

3. Какая модель аутентификации принята в компании?
   - Entra ID;
   - Keycloak;
   - API keys;
   - mutual TLS.

4. Нужен ли semantic search в первой версии?
   - Да / нет / позже.

5. Есть ли требования по RBAC на уровне отдельных страниц?

6. Где будет хоститься сервис?
   - Kubernetes;
   - Docker Compose;
   - Windows VM;
   - Cloud managed service.

7. Какой допустимый возраст данных wiki?
   - near real-time;
   - 15 минут;
   - 1 час;
   - 1 сутки.

8. Нужен ли offline fallback?

9. Какой максимальный размер ответа допустим для harness?

10. Нужен ли `wiki_ask`, или достаточно search/get page?

---

## 27. Рекомендуемый MVP

Если нужно быстро начать, рекомендуется такой минимальный scope:

### Backend

- .NET 10 LTS, C# 14, `LangVersion=14`.
- ASP.NET Core Minimal API.
- MCP endpoint (`ModelContextProtocol.AspNetCore`).
- FileSystem или Git connector.
- PostgreSQL FTS или Meilisearch.
- JWT / API key auth.
- `Abdt.Infrastructure.Logging` (логирование с маскированием ПД).
- `Abdt.Infrastructure.Monitoring` (health checks: `/health`, `/health/full`).
- `Abdt.Infrastructure.RateLimiting` (rate limiting, Redis).
- Quality Gates: `Protoobp.Trace.Bundle`, `Abdt.Infrastructure.OpenTelemetry.Metrics`, `Abdt.Infrastructure.CodeAnalysis.Configuration`.
- CPM (`Directory.Packages.props`).
- Двухэтапный Dockerfile (ProtoOBP log dir).

### Tools

```text
wiki_search
wiki_get_page
```

### Harness integration

Если есть remote MCP:

```json
{
  "mcpServers": {
    "wiki": {
      "url": "https://wiki-mcp.corp.local/mcp",
      "headers": {
        "Authorization": "Bearer ${WIKI_MCP_TOKEN}"
      }
    }
  }
}
```

Если только stdio:

```json
{
  "mcpServers": {
    "wiki": {
      "command": "dotnet",
      "args": [
        "WikiMcp.Proxy.dll",
        "--url",
        "https://wiki-mcp.corp.local/mcp"
      ]
    }
  }
}
```

---

## 28. Пример реализации на .NET

> Ниже — иллюстративный пример, не финальный код. Паттерны: Entry-регистрация, ABDT-пакеты, Guard, `internal sealed record` DTO, XML-комментарии, `CancellationToken`, Vertical Slice.

### 28.1. Program.cs (composition root)

```csharp
using WikiMcp.Server.Mcp;

var builder = WebApplication.CreateBuilder(args);

// Configuration validation (ABDT)
builder.Configuration.WithAbdtValidation();

// Logging (ABDT)
builder.Services.AddRequestLogging(
    options => options.CategoryName = "WikiMcp");

// Quality Gates (обязательные)
builder.Services.AddOpenTelemetrySystemMetrics(builder.Configuration);

// Infrastructure layers via Entry-pattern
builder.Services
    .AddDomain()
    .AddInfrastructure(builder.Configuration)
    .AddSearch(builder.Configuration)
    .AddConnectors(builder.Configuration)
    .AddWikiMcpServer();

// MCP server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<WikiTools>();

// Authorization
builder.Services.AddAuthorization();

// Rate limiting (ABDT — двухуровневый: general DDoS + business)
builder.Services.AddRateLimiting(o => builder.Configuration.GetSection("App:RateLimiting").Bind(o));

// Health checks (ABDT — auto-generation by IConfiguration)
builder.Services.AddAutoHealthChecks(builder.Configuration);

var app = builder.Build();

// Pipeline
app.UseRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiting();  // после UseRouting

// Health endpoints: /health, /health/full, /health/zabbix
app.UseAutoHealthChecks("wiki-mcp");

app.MapMcp("/mcp");

app.Run();
```

### 28.2. Wiki tools (MCP)

```csharp
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using ModelContextProtocol.Server;

namespace WikiMcp.Server.Mcp;

/// <summary>
/// MCP tools for wiki search and page retrieval.
/// </summary>
[McpServerToolType]
internal sealed class WikiTools
{
    private readonly IWikiSearchService _searchService;
    private readonly IWikiPageService _pageService;

    public WikiTools(IWikiSearchService searchService, IWikiPageService pageService)
    {
        _searchService = searchService;
        _pageService = pageService;
    }

    /// <summary>
    /// Search internal LLM wiki.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="topK">Number of results (default 5, max 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search results.</returns>
    [McpServerTool]
    [Description("Search internal LLM wiki")]
    public async Task<IReadOnlyList<WikiSearchResult>> WikiSearch(
        [Description("Search query")] string query,
        [Description("Number of results")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(query, nameof(query));
        Guard.IsGreaterThan(topK, 0, nameof(topK));

        // Server-side limit
        topK = Math.Min(topK, 20);

        return await _searchService.SearchAsync(query, topK, cancellationToken);
    }

    /// <summary>
    /// Get wiki page by path or id.
    /// </summary>
    /// <param name="page">Page path or id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Wiki page content.</returns>
    [McpServerTool]
    [Description("Get wiki page by path or id")]
    public async Task<WikiPageDto> WikiGetPage(
        [Description("Page path or id")] string page,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(page, nameof(page));

        return await _pageService.GetPageAsync(page, cancellationToken);
    }
}
```

### 28.3. DTO (internal sealed record)

```csharp
namespace WikiMcp.Server.Mcp;

/// <summary>
/// Single wiki search result.
/// </summary>
internal sealed record WikiSearchResult
{
    /// <summary>Unique page identifier.</summary>
    public required string PageId { get; init; }

    /// <summary>Page path or slug.</summary>
    public required string Path { get; init; }

    /// <summary>Page title.</summary>
    public required string Title { get; init; }

    /// <summary>Relevant snippet.</summary>
    public required string Snippet { get; init; }

    /// <summary>Relevance score (0.0 to 1.0).</summary>
    public double Score { get; init; }

    /// <summary>Last update timestamp (UTC).</summary>
    public required Instant UpdatedAt { get; init; }

    /// <summary>Page revision identifier.</summary>
    public string? Revision { get; init; }
}

/// <summary>
/// Full wiki page content.
/// </summary>
internal sealed record WikiPageDto
{
    /// <summary>Unique page identifier.</summary>
    public required string PageId { get; init; }

    /// <summary>Page path or slug.</summary>
    public required string Path { get; init; }

    /// <summary>Page title.</summary>
    public required string Title { get; init; }

    /// <summary>Full page content in Markdown.</summary>
    public required string ContentMarkdown { get; init; }

    /// <summary>Last update timestamp (UTC).</summary>
    public required Instant UpdatedAt { get; init; }

    /// <summary>Page revision identifier.</summary>
    public string? Revision { get; init; }
}
```

> **Примечание:** `Instant` — из `NodaTime` (ABDT-стандарт для temporal types). Не использовать `DateTime`/`DateTimeOffset` в DTO доменной модели.

### 28.4. Vertical Slice — Search feature

```csharp
using FluentValidation;

namespace WikiMcp.Server.Features.Search;

/// <summary>
/// wiki_search endpoint and handler.
/// </summary>
internal sealed class Search : EndpointBase
{
    protected override string Name => "wiki_search";

    protected override RouteHandlerBuilder MapEndpointCore(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v{version:apiVersion}/wiki/search", Handle)
            .AddEndpointFilter<ValidationFilter<SearchRequest>>()
            .WithApiVersioning();
    }

    private static async Task<Results<Ok<IReadOnlyList<WikiSearchResult>>, ValidationProblem>> Handle(
        [FromBody] SearchRequest request,
        IWikiSearchService searchService,
        CancellationToken cancellationToken)
    {
        var results = await searchService.SearchAsync(request.Query, request.TopK, cancellationToken);
        return TypedResults.Ok(results);
    }
}

/// <summary>
/// Search request DTO.
/// </summary>
internal sealed record SearchRequest
{
    /// <summary>Search query.</summary>
    public required string Query { get; init; }

    /// <summary>Number of results (default 5, max 20).</summary>
    public int TopK { get; init; } = 5;
}

/// <summary>
/// Validator for SearchRequest.
/// </summary>
internal sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 20)
            .WithMessage("TopK must be between 1 and 20.");
    }
}
```

---

## 29. Рекомендуемое определение готовности / Definition of Done

Проект считается готовым для пилота, если:

1. MCP-сервер развёрнут в DEV/TEST.
2. Harness успешно подключается.
3. Доступны tools `wiki_search` и `wiki_get_page`.
4. Поиск возвращает актуальные результаты.
5. Страницы читаются по идентификатору/пути.
6. Авторизация работает.
7. Ошибки понятны и стандартизированы.
8. Логи и метрики собираются.
9. Нет необходимости хранить wiki на клиенте.
10. Пройден security review для пилотного контура.
11. Подготовлены runbook и инструкция подключения.

---

## 30. Итоговая рекомендация

Для реализации на .NET рекомендуется начать с MVP, выровненный по ABDT-стандартам:

1. ASP.NET Core MCP server (`ModelContextProtocol.AspNetCore`).
2. Один источник wiki (FileSystem или Git connector).
3. Полнотекстовый поиск (PostgreSQL FTS).
4. Tools: `wiki_search`, `wiki_get_page`.
5. JWT / API key auth.
6. ABDT-инфраструктура:
   - `Abdt.Infrastructure.Logging` — логирование с маскированием ПД;
   - `Abdt.Infrastructure.Monitoring` — health checks (`/health`, `/health/full`);
   - `Abdt.Infrastructure.RateLimiting` — rate limiting (Redis, двухуровневый);
   - Quality Gates: `Protoobp.Trace.Bundle`, `Abdt.Infrastructure.OpenTelemetry.Metrics`, `Abdt.Infrastructure.CodeAnalysis.Configuration`.
7. CPM (`Directory.Packages.props`), `Directory.Build.props` (`TreatWarningsAsErrors`).
8. Двухэтапный Docker deployment (ProtoOBP log dir, Artifactory).
9. GitLab CI/CD (`cicd/gitlabci-templates`, `dotNET_VERSION=10.0`).
10. Подключение пилотного harness.

Это даст быстрый эффект: harness перестанет зависеть от локальной копии wiki, а знания станут централизованными, управляемыми и аудируемыми — с полным соответствием ABDT Quality Gates.