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
/// Headless-СЂРµР¶РёРј Р·Р°РїСѓСЃРєР° VantuzLauncher.
/// РЎРѕРіР»Р°СЃРЅРѕ Armatura: SRP (С‚РѕР»СЊРєРѕ Р»РѕРіРёРєР° Р·Р°РїСѓСЃРєР°), Nomadic (РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅС‹Рµ РїСѓС‚Рё),
/// Composability (JSON-РѕС‚С‡С‘С‚ РґР»СЏ РёРЅС‚РµРіСЂР°С†РёРё).
/// </summary>
/// F_doc: {HeadlessRunner returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies HeadlessRunner behavior
public static class HeadlessRunner
{
    /// F_doc: {HeadlessOptions returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies HeadlessOptions behavior
    public record HeadlessOptions
    {
        /// F_doc: {Username returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Username behavior
        public string Username { get; init; } = string.Empty;
        /// F_doc: {Password returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Password behavior
        public string Password { get; init; } = string.Empty;
        /// F_doc: {RamMb returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies RamMb behavior
        public int RamMb { get; init; } = 4096;
        public string? TestCredentialsPath { get; init; }
        /// F_doc: {TestMode returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies TestMode behavior
        public bool TestMode { get; init; } = false;
        public string? BootPath { get; init; }
        public string? WorkspacePath { get; init; }
    }
/// F_doc: {TestResult returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies TestResult behavior

    public record TestResult
    {
        /// F_doc: {Success returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Success behavior
        public bool Success { get; init; }
        /// F_doc: {Status returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Status behavior
        public string Status { get; init; } = "unknown";
        public string? ErrorMessage { get; init; }
        /// F_doc: {StartTime returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies StartTime behavior
        public DateTime StartTime { get; init; }
        /// F_doc: {EndTime returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies EndTime behavior
        public DateTime EndTime { get; init; }
        public TimeSpan Duration => EndTime - StartTime;
        /// F_doc: {Logs returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Logs behavior
        public List<string> Logs { get; init; } = new();
        public Dictionary<string, object>? FinalPayload { get; init; }
    }

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ РїРѕР»РЅС‹Р№ С†РёРєР» Р»Р°СѓРЅС‡РµСЂР° РІ headless-СЂРµР¶РёРјРµ.
    /// </summary>
    public static async Task<TestResult> RunAsync(HeadlessOptions options, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var logs = new List<string>();
        TestReporter reporter = new(logs);

        try
        {
            string mcDir = options.WorkspacePath ?? AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(mcDir, "launcher_config.json");
            string bootJsonPath = !string.IsNullOrEmpty(options.BootPath) 
                ? options.BootPath 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, options.TestMode ? "boot.test.json" : "boot.json");
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            string crashLogPath = Path.Combine(mcDir, "crash.log");

            // РџСЂРѕРІРµСЂРєР° РЅР°Р»РёС‡РёСЏ boot.json
            if (!File.Exists(bootJsonPath))
            {
                return CreateResult(false, "boot.json not found", startTime, logs);
            }

            // РЎРѕС…СЂР°РЅСЏРµРј С‚РµСЃС‚РѕРІС‹Рµ credentials РµСЃР»Рё РЅСѓР¶РЅРѕ
            if (!string.IsNullOrEmpty(options.TestCredentialsPath))
            {
                var testCreds = new { options.Username, options.Password };
                File.WriteAllText(options.TestCredentialsPath, JsonSerializer.Serialize(testCreds));
            }

            // РџРѕРґРіРѕС‚РѕРІРєР° payload
            var initialPayload = new Dictionary<string, object>
            {
                ["username"] = options.Username,
                ["password"] = options.Password,
                ["ramMb"] = options.RamMb,
                ["workspace"] = mcDir
            };

            reporter.ReportState("Initializing VantuzEngine...");

            // Р—Р°РїСѓСЃРє РґРІРёР¶РєР°
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
    /// РЎРѕС…СЂР°РЅСЏРµС‚ СЂРµР·СѓР»СЊС‚Р°С‚ С‚РµСЃС‚Р° РІ JSON-С„Р°Р№Р».
    /// </summary>
    /// F_doc: {SaveResult returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies SaveResult behavior
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
    /// Reporter РґР»СЏ headless-СЂРµР¶РёРјР° вЂ” РїРёС€РµС‚ РІ Р»РѕРіРё РІРјРµСЃС‚Рѕ UI.
    /// </summary>
    private class TestReporter : IStatusReporter
    {
        private readonly List<string> _logs;

        public TestReporter(List<string> logs)
        {
            _logs = logs;
        }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

        public void ReportProgress(string taskName, double percentage)
        {
            string msg = $"[{DateTime.UtcNow:HH:mm:ss}] {taskName}: {percentage:F1}%";
            _logs.Add(msg);
            Console.WriteLine(msg);
        }
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

        public void ReportState(string message)
        {
            string msg = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
            _logs.Add(msg);
            Console.WriteLine(msg);
        }
    }
}
