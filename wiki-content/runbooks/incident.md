---
title: Разбор инцидента
namespace: runbooks
tags:
  - incident
---

# Разбор инцидента

Что делать, если harness получает устаревшие данные из wiki.

1. Проверить `GET /api/v1/admin/sync/status` — статус и время последней синхронизации.
2. Если статус `Failed`, посмотреть поле `error`.
3. Запустить полное переиндексирование: `POST /api/v1/admin/sync?full=true`.
