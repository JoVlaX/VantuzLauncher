namespace VantuzLauncher;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
using Vantuz.Host;

/// <summary>
/// Headless-режим запуска VantuzLauncher.
/// Согласно Armatura: SRP (только логика запуска), Nomadic (относительные пути),
/// Composability (JSON-отчёт для интеграции).
/// </summary>
public static class HeadlessRunner
{
    public record HeadlessOptions
    {
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public int RamMb { get; init; } = 4096;
        public string? TestCredentialsPath { get; init; }
    }

    public record TestResult
    {
        public bool Success { get; init; }
        public string Status { get; init; } = "unknown";
        public string? ErrorMessage { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> Logs { get; init; } = new();
        public Dictionary<string, object>? FinalPayload { get; init; }
    }

    /// <summary>
    /// Запускает полный цикл лаунчера в headless-режиме.
    /// </summary>
    public static async Task<TestResult> RunAsync(HeadlessOptions options, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var logs = new List<string>();
        TestReporter reporter = new(logs);

        try
        {
            string mcDir = App.WorkspacePath;
            string configPath = Path.Combine(mcDir, "launcher_config.json");
            string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.json");
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            string crashLogPath = Path.Combine(mcDir, "crash.log");

            // Проверка наличия boot.json
            if (!File.Exists(bootJsonPath))
            {
                return CreateResult(false, "boot.json not found", startTime, logs);
            }

            // Сохраняем тестовые credentials если нужно
            if (!string.IsNullOrEmpty(options.TestCredentialsPath))
            {
                var testCreds = new { options.Username, options.Password };
                File.WriteAllText(options.TestCredentialsPath, JsonSerializer.Serialize(testCreds));
            }

            // Подготовка payload
            var initialPayload = new Dictionary<string, object>
            {
                ["username"] = options.Username,
                ["password"] = options.Password,
                ["ramMb"] = options.RamMb
            };

            reporter.ReportState("Initializing VantuzEngine...");

            // Запуск движка
            var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);
            var result = await engine.RunAsync(bootJsonPath, cancellationToken, initialPayload);

            if (!result.Success)
            {
                return CreateResult(false, result.ErrorMessage ?? "Engine execution failed", startTime, logs, result.Payload);
            }

            return CreateResult(true, null, startTime, logs, result.Payload);
        }
        catch (OperationCanceledException)
        {
            return CreateResult(false, "Operation cancelled", startTime, logs);
        }
        catch (Exception ex)
        {
            return CreateResult(false, $"{ex.GetType().Name}: {ex.Message}", startTime, logs);
        }
    }

    private static TestResult CreateResult(bool success, string? error, DateTime startTime, List<string> logs, IReadOnlyDictionary<string, object>? payload = null)
    {
        var result = new TestResult
        {
            Success = success,
            Status = success ? "success" : "failed",
            ErrorMessage = error,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            Logs = logs
        };

        if (payload != null)
        {
            result = result with
            {
                FinalPayload = new Dictionary<string, object>(payload)
            };
        }

        return result;
    }

    /// <summary>
    /// Сохраняет результат теста в JSON-файл.
    /// </summary>
    public static void SaveResult(TestResult result, string outputPath)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Reporter для headless-режима — пишет в логи вместо UI.
    /// </summary>
    private class TestReporter : IStatusReporter
    {
        private readonly List<string> _logs;

        public TestReporter(List<string> logs)
        {
            _logs = logs;
        }

        public void ReportProgress(string taskName, double percentage)
        {
            string msg = $"[{DateTime.UtcNow:HH:mm:ss}] {taskName}: {percentage:F1}%";
            _logs.Add(msg);
            Console.WriteLine(msg);
        }

        public void ReportState(string message)
        {
            string msg = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
            _logs.Add(msg);
            Console.WriteLine(msg);
        }
    }
}
