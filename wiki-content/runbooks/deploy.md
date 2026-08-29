---
title: Deployment runbook
namespace: runbooks
tags:
  - deploy
  - harness
updatedAt: 2026-08-01T10:15:00Z
---

# Развёртывание сервиса

Порядок выкладки сервиса через harness.

## Предусловия

- Собран container image и запушен в Artifactory.
- Применены миграции базы данных.

## Шаги

1. Проверить состояние синхронизации: `GET /api/v1/admin/sync/status`.
2. Обновить образ в Kubernetes.
3. Дождаться готовности: `GET /health/full` должен вернуть `Healthy`.
4. Прогнать смоук-тест поиска по wiki.

## Откат

Вернуть предыдущий тег образа и повторить проверку health.
