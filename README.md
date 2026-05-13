# VantuzLauncher 2.0 — Агностический Payload-Driven Бутстраппер

**VantuzLauncher 2.0** — это глубоко переработанная, модульная архитектура лаунчера, построенная на принципах **Microkernel** и **Middleware Pipeline**. В отличие от классических монолитных лаунчеров, Vantuz 2.0 является «тупым» исполнителем, чья логика полностью определяется внешним JSON-манифестом (`boot.json`) и набором независимых плагинов-лезвий.

---

## 🚀 Ключевые особенности (Архитектура 2.0)

- **Payload-Driven Execution**: Вся бизнес-логика (авторизация, загрузка, запуск) описана в JSON. Лаунчер лишь исполняет цепочку шагов.
- **Microkernel Architecture**: Ядро (`Vantuz.Host`) изолировано от конкретных реализаций. Плагины загружаются в отдельные контексты (`AssemblyLoadContext`).
- **Hash Pinning & Security**: Все плагины проходят проверку SHA-256 перед загрузкой. Подмена DLL невозможна.
- **Memory Loading**: Плагины загружаются напрямую в память, не блокируя файлы на диске.
- **Variable Interpolation**: Поддержка динамических переменных в конфиге (например, `{{mcDir}}`, `{{gameArgs}}`).
- **Thread-Safe Reporting**: Потокобезопасная передача состояния из фонового конвейера в UI WPF через `IStatusReporter`.

---

## 📂 Структура проекта

- **VantuzLauncher**: WPF-приложение (UI). Собирает данные, инициализирует ядро.
- **Vantuz.Core**: Общие контракты, интерфейсы плагинов и контекст выполнения.
- **Vantuz.Host**: Ядро (Engine). Отвечает за загрузку плагинов, валидацию хэшей и работу конвейера.
- **Vantuz.Builder**: CLI-утилита для автоматической сборки `boot.json` и вычисления хэшей плагинов.
- **Vantuz.Plugins.***: Набор независимых лезвий:
  - `Auth.Yggdrasil`: Авторизация по протоколу Yggdrasil.
  - `Net.Downloader`: Универсальный загрузчик файлов.
  - `OS.Executor`: Запуск процессов с перехватом вывода.
  - `Game.CmlLaunch`: Подготовка Minecraft (на базе CmlLib.Core).

---

## 🛠 Инструкция по сборке

Проект полностью автоматизирован через MSBuild. При сборке основного проекта `VantuzLauncher` автоматически собираются все плагины, копируются в папку `plugins` и генерируется актуальный `boot.json`.

1. Убедитесь, что установлен **.NET 8.0 SDK**.
2. Клонируйте репозиторий.
3. Выполните сборку решения:
   ```bash
   dotnet build VantuzLauncher.sln
   ```
4. Результат будет находиться в:
   `VantuzLauncher/bin/Debug/net8.0-windows/win-x64/`

---

## ⚙️ Конфигурация (boot.json)

Логика работы описывается в `boot.template.json`. Пример цепочки (Pipeline):

```json
{
  "plugins": { "Vantuz.Plugins.Auth.dll": "" }, // Хэши заполняются автоматически
  "pipeline": [
    {
      "pluginName": "Auth.Yggdrasil",
      "config": { "url": "https://api.authserver.com/authenticate" }
    },
    {
      "pluginName": "Net.Downloader",
      "config": { 
        "url": "https://example.com/file.jar", 
        "destination": "{{mcDir}}/file.jar" 
      }
    }
  ]
}
```

---

## 🔒 Безопасность

- **Изоляция**: Плагины не имеют прямого доступа к UI. Взаимодействие идет только через `ExecutionContext`.
- **Криптография**: Локальные пароли шифруются AES с привязкой к железу пользователя (`MachineName`).
- **Resilience**: Любой сбой в плагине корректно обрабатывается ядром через `context.Abort()`, не вызывая краш всего приложения.

---

## 👨‍💻 Для разработчиков плагинов

Чтобы создать новое лезвие, реализуйте интерфейс `IVantuzPlugin` из `Vantuz.Core`:

```csharp
public class MyPlugin : IVantuzPlugin 
{
    public string Name => "My.CustomPlugin";
    public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
    {
        // Ваша логика здесь
        await next(context); // Передать управление дальше
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---
*VantuzLauncher — Думай как Senior, делай код чистым.*
