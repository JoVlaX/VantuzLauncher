namespace Vantuz.Plugins.OS;

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Command: Запуск исполняемых процессов.
/// Per Armatura:76-78 - только запись/модификация состояния (запуск процесса).
/// </summary>
public class ExecuteCommand : ICommandPlugin
{
    public string Name => "OS.ExecuteCommand"; 
 
    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        string fileName = stepConfig.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException("fileName is missing"); 
         
        string arguments = stepConfig.TryGetProperty("arguments", out var argsProp) ? argsProp.GetString() ?? "" : ""; 
        string workDir = stepConfig.TryGetProperty("workDir", out var wdProp) ? wdProp.GetString() ?? AppContext.BaseDirectory : AppContext.BaseDirectory; 
        bool waitForExit = stepConfig.TryGetProperty("waitForExit", out var waitProp) ? waitProp.GetBoolean() : true; 

        // Интерполяция переменных: заменяем {{key}} на значения из Payload конвейера 
        fileName = Interpolate(fileName, context); 
        arguments = Interpolate(arguments, context); 
        workDir = Interpolate(workDir, context); 

        context.Reporter.ReportState($"[ExecuteCommand] waitForExit={waitForExit}, fileName={fileName}, workDir={workDir}");

        if (fileName.Contains("{{") || arguments.Contains("{{") || workDir.Contains("{{"))
        {
            context.Reporter.ReportState("[WARN] OS.ExecuteCommand arguments contain unresolved {{...}} placeholders after interpolation.");
        }

        // Skip real process launch in dry-run / test mode per INVARIANT_THEORY.md §1.2
        if (stepConfig.TryGetProperty("dryRun", out var dr) && dr.GetBoolean())
        {
            context.Reporter.ReportState($"[DRY RUN] Would execute: {Path.GetFileName(fileName)} {arguments} (workDir: {workDir})");
            return new CommandResult(true);
        }
 
        if (!File.Exists(fileName) && !IsSystemCommand(fileName))
        {
            return new CommandResult(false, $"Исполняемый файл не найден: {fileName}");
        } 
 
        context.Reporter.ReportState($"Запуск: {Path.GetFileName(fileName)}..."); 
 
        var startInfo = new ProcessStartInfo 
        { 
            FileName = fileName, 
            Arguments = arguments, 
            WorkingDirectory = workDir, 
            UseShellExecute = false, 
            CreateNoWindow = true 
        }; 

        if (waitForExit)
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        var process = new Process { StartInfo = startInfo };

        var stderrBuilder = new System.Text.StringBuilder();

        if (waitForExit)
        {
            // Перенаправляем вывод процесса в наш UI через Reporter
            process.OutputDataReceived += (sender, e) => {
                if (!string.IsNullOrWhiteSpace(e.Data)) context.Reporter.ReportState($"[OUT] {e.Data}");
            };
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    context.Reporter.ReportState($"[ERR] {e.Data}");
                    stderrBuilder.AppendLine(e.Data);
                }
            }; 
        }
 
        try 
        { 
            process.Start(); 

            if (waitForExit)
            {
                process.BeginOutputReadLine(); 
                process.BeginErrorReadLine(); 
                await process.WaitForExitAsync(context.CancellationToken);

                if (process.ExitCode != 0)
                {
                    var stderr = stderrBuilder.ToString().Trim();
                    var details = string.IsNullOrEmpty(stderr)
                        ? $""
                        : $"\nStderr:\n{stderr}";
                    return new CommandResult(false, $"Процесс {Path.GetFileName(fileName)} завершился с ошибкой (ExitCode: {process.ExitCode}){details}");
                }
            }
            else
            {
                // Fire-and-forget: do not redirect streams and do not Dispose while child is alive.
                // Give process a brief grace period to detect immediate crash.
                try
                {
                    await Task.Delay(2000, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Pipeline shutting down, ignore
                }

                if (process.HasExited && process.ExitCode != 0)
                {
                    return new CommandResult(false, $"Процесс крашнулся при запуске (ExitCode: {process.ExitCode})");
                }
            } 
        } 
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CommandResult(false, $"Ошибка запуска процесса: {ex.Message}");
        }
        finally
        {
            if (waitForExit)
            {
#pragma warning disable ARM010 // Local Process variable; Dispose required to release handles after WaitForExit.
                process.Dispose();
#pragma warning restore ARM010
            }
            // If waitForExit == false, we intentionally do NOT dispose the Process
            // while the child may still be running. .NET finalizer will reclaim handles.
        }

        return new CommandResult(true); 
    } 
 
    private static string Interpolate(string text, CommandContext context) 
    {
        if (string.IsNullOrEmpty(text)) return text;
        var mutations = context.GetMutations();
        foreach (var kvp in mutations)
        {
            text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }
        return text;
    } 
 
    private static bool IsSystemCommand(string fileName) 
    { 
        // Простая эвристика для пропуска проверки File.Exists для системных команд вроде "java" или "cmd" 
        return !fileName.Contains('/') && !fileName.Contains('\\') && !fileName.EndsWith(".exe"); 
    } 
 
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
} 
