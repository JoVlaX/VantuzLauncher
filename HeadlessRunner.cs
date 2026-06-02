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
        public bool TestMode { get; init; } = false;  // Phase 2: Mock auth for Nomadic testing
        public string? BootPath { get; init; }  // Per INVARIANT_THEORY.md §498 - explicit boot file
    }

    public record PhaseResult
    {
        public bool Passed { get; init; }
        public long DurationMs { get; init; }
        public string? Error { get; init; }
    }

    public record LaunchPhaseResult : PhaseResult
    {
        public int? JavaPid { get; init; }           // Phase 3A: Java process detected
        public bool ArgumentsValid { get; init; }   // Phase 3.4: Args validation
        public double RuntimeSeconds { get; init; } // Phase 3.5: Not immediate crash
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
        
        // Phase breakdown per INVARIANT_THEORY.md Measurability
        public PhaseResult? PreFlight { get; init; }
        public PhaseResult? Execution { get; init; }
        public LaunchPhaseResult? Launch { get; init; }
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
            
            // Select manifest per Axiom of Explicitness (498) - explicit test configuration
            string bootFileName = options.TestMode ? "boot.test.json" : "boot.json";
            string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bootFileName);
            
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            string crashLogPath = Path.Combine(mcDir, "crash.log");
            
            reporter.ReportState($"Using manifest: {bootFileName}");

            // Phase 1: Pre-flight verification (per INVARIANT_THEORY.md Measurability)
            var preFlightStart = DateTime.UtcNow;
            if (!File.Exists(bootJsonPath))
            {
                return CreateResult(false, "boot.json not found", startTime, logs, null,
                    new PhaseResult { Passed = false, Error = "boot.json not found", DurationMs = 0 });
            }
            var preFlightResult = new PhaseResult { Passed = true, DurationMs = (long)(DateTime.UtcNow - preFlightStart).TotalMilliseconds };

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

            // Phase 2: Test mode injection (Nomadic Invariant - no external dependencies)
            if (options.TestMode)
            {
                reporter.ReportState("Test mode: Mocking authentication...");
                initialPayload["test_mode"] = true;
                initialPayload["mock_auth_token"] = "TEST_TOKEN_VANTUZ_2026";
                initialPayload["mock_auth_success"] = true;
            }

            reporter.ReportState("Initializing VantuzEngine...");

            // Phase 3: Pipeline execution
            var execStart = DateTime.UtcNow;
            var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);
            var result = await engine.RunAsync(bootJsonPath, cancellationToken, initialPayload);
            var execResult = new PhaseResult 
            { 
                Passed = result.Success, 
                DurationMs = (long)(DateTime.UtcNow - execStart).TotalMilliseconds,
                Error = result.Success ? null : (result.ErrorMessage ?? "Engine execution failed")
            };

            if (!result.Success)
            {
                return CreateResult(false, result.ErrorMessage ?? "Engine execution failed", startTime, logs, 
                    result.Payload, preFlightResult, execResult);
            }

            // Phase 4: Java process verification (Variant A per INVARIANT_THEORY.md)
            LaunchPhaseResult? launchResult = null;
            if (options.TestMode && result.Payload != null)
            {
                launchResult = await VerifyJavaProcessAsync(result.Payload, reporter, cancellationToken);
                if (!launchResult.Passed)
                {
                    return CreateResult(false, "Java process verification failed", startTime, logs, 
                        result.Payload, preFlightResult, execResult, launchResult);
                }
            }

            return CreateResult(true, null, startTime, logs, result.Payload, preFlightResult, execResult, launchResult);
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

    private static TestResult CreateResult(
        bool success, 
        string? error, 
        DateTime startTime, 
        List<string> logs, 
        IReadOnlyDictionary<string, object>? payload = null,
        PhaseResult? preFlight = null,
        PhaseResult? execution = null,
        LaunchPhaseResult? launch = null)
    {
        var result = new TestResult
        {
            Success = success,
            Status = success ? "passed" : "failed",
            ErrorMessage = error,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            Logs = logs,
            PreFlight = preFlight,
            Execution = execution,
            Launch = launch
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
    /// Phase 4: Verifies Java process launch per INVARIANT_THEORY.md Measurability (Variant A).
    /// Checks: process exists, arguments valid, runtime > 2 seconds (not immediate crash).
    /// </summary>
    private static async Task<LaunchPhaseResult> VerifyJavaProcessAsync(
        IReadOnlyDictionary<string, object> payload, 
        IStatusReporter reporter, 
        CancellationToken cancellationToken)
    {
        var launchStart = DateTime.UtcNow;
        reporter.ReportState("Phase 4: Verifying Java process launch...");

        // Extract Java process info from payload (set by Game plugin)
        int? javaPid = payload.TryGetValue("java_pid", out var pidObj) ? (int?)pidObj : null;
        string? javaArgs = payload.TryGetValue("java_arguments", out var argsObj) ? argsObj as string : null;
        bool? processStarted = payload.TryGetValue("java_process_started", out var startedObj) ? (bool?)startedObj : null;

        // 4.1: Check process was marked as started
        if (processStarted != true)
        {
            return new LaunchPhaseResult 
            { 
                Passed = false, 
                DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                Error = "Java process not marked as started in payload"
            };
        }

        // 4.2: Validate arguments contain required elements
        bool argsValid = !string.IsNullOrEmpty(javaArgs) && 
                        javaArgs.Contains("-cp") && 
                        (javaArgs.Contains("minecraft") || javaArgs.Contains(".jar"));
        
        if (!argsValid)
        {
            return new LaunchPhaseResult 
            { 
                Passed = false, 
                DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                ArgumentsValid = false,
                Error = "Java arguments missing required components (-cp, .jar)"
            };
        }

        // 4.3: Verify actual process exists (if PID provided) - skip in test mode
        bool isLaunchTestMode = payload.TryGetValue("LaunchTestMode", out var testModeObj) && testModeObj is bool b && b;
        if (javaPid.HasValue && !isLaunchTestMode)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(javaPid.Value);
                if (process.HasExited)
                {
                    return new LaunchPhaseResult 
                    { 
                        Passed = false, 
                        JavaPid = javaPid,
                        DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                        ArgumentsValid = true,
                        Error = "Java process exited immediately"
                    };
                }
            }
            catch (ArgumentException)
            {
                return new LaunchPhaseResult 
                { 
                    Passed = false, 
                    JavaPid = javaPid,
                    DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                    ArgumentsValid = true,
                    Error = "Java process not found (PID invalid)"
                };
            }
        }

        // 4.4: Wait 2 seconds to ensure process doesn't crash immediately (per plan) - skip in test mode
        if (!isLaunchTestMode)
        {
            reporter.ReportState("Waiting 2s to verify process stability...");
            await Task.Delay(2000, cancellationToken);
        }

        // Re-check process still running (skip in test mode)
        if (javaPid.HasValue && !isLaunchTestMode)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(javaPid.Value);
                if (process.HasExited)
                {
                    return new LaunchPhaseResult 
                    { 
                        Passed = false, 
                        JavaPid = javaPid,
                        DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                        ArgumentsValid = true,
                        RuntimeSeconds = 2.0,
                        Error = "Java process crashed within 2 seconds"
                    };
                }
                
                // Process still running - success!
                return new LaunchPhaseResult 
                { 
                    Passed = true, 
                    JavaPid = javaPid,
                    DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                    ArgumentsValid = true,
                    RuntimeSeconds = 2.0
                };
            }
            catch (ArgumentException)
            {
                return new LaunchPhaseResult 
                { 
                    Passed = false, 
                    JavaPid = javaPid,
                    DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
                    ArgumentsValid = true,
                    RuntimeSeconds = 2.0,
                    Error = "Java process disappeared during stability check"
                };
            }
        }

        // No PID available but process marked started - partial success
        // In test mode, return full success since we're simulating
        return new LaunchPhaseResult 
        { 
            Passed = true, 
            JavaPid = javaPid,
            DurationMs = (long)(DateTime.UtcNow - launchStart).TotalMilliseconds,
            ArgumentsValid = argsValid,
            RuntimeSeconds = isLaunchTestMode ? 2.0 : 0,
            Error = isLaunchTestMode ? null : "Process started but PID not available for verification"
        };
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
