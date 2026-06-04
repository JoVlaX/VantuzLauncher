---
description: Automated testing pipeline for VantuzLauncher
---

# VantuzLauncher Test & Run Workflow

Автономный тестовый пайплайн для VantuzLauncher с соблюдением принципов Armatura.

## Usage

```powershell
# Полный цикл: сборка + запуск + отчёт
.\test-and-run.ps1

# С пользовательскими credentials
.\test-and-run.ps1 -Username "myuser" -Password "mypass" -Ram 8192

# Только запуск (без сборки) — быстрее при повторных тестах
.\test-and-run.ps1 -NoBuild

# Увеличенный таймаут для медленных систем
.\test-and-run.ps1 -Timeout 600
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Build failed |
| 2 | Runtime error |
| 3 | Timeout |

## Files

- `test-and-run.ps1` — PowerShell оркестратор
- `HeadlessRunner.cs` — Headless-режим лаунчера (SRP)
- `test-result.json` — JSON-результат последнего теста
- `test-report.log` — Human-readable отчёт

## Architecture Principles

**SRP:**
- `HeadlessRunner.cs` — только логика запуска
- `test-and-run.ps1` — только оркестрация пайплайна
- JSON-выход — только форматирование результата

**Nomadic:**
- Все пути относительны `$PSScriptRoot`
- Работает из любой директории
- Нет абсолютных путей в коде

**Composability:**
- Exit codes для интеграции с CI/CD
- JSON-формат для машинной обработки
- Модульная структура скрипта

## Integration with Cascade

Cascade может автоматически выполнять этот пайплайн после изменений:

```powershell
# В рабочей директории проекта:
& "c:\000\projects\compositum\test-and-run.ps1" -Username "test" -Password "test"

# Проверка результата:
$result = Get-Content "c:\000\projects\compositum\test-result.json" | ConvertFrom-Json
if ($result.status -eq "success") { "Tests passed!" }
```
