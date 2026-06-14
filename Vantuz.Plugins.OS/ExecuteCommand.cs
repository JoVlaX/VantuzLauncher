namespace Vantuz.Plugins.OS;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Command: Р—Р°РїСѓСЃРє РёСЃРїРѕР»РЅСЏРµРјС‹С… РїСЂРѕС†РµСЃСЃРѕРІ.
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ Р·Р°РїРёСЃСЊ/РјРѕРґРёС„РёРєР°С†РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ (Р·Р°РїСѓСЃРє РїСЂРѕС†РµСЃСЃР°).
/// F_doc: {executable not found, process exits with non-zero code, or stdout contains error}
/// E_doc: Unit test with mock Process verifying exit code and output capture
/// </summary>
public class ExecuteCommand : ICommandPlugin
{
    public string Name => "OS.ExecuteCommand"; 
 
    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Honor GUI cancellation override if present (set by MinecraftLauncherGUIPlugin)
        CancellationToken effectiveToken = context.Get<CancellationToken>("cancellation_token") is CancellationToken guiToken
            ? guiToken
            : context.CancellationToken;

        string fileName = stepConfig.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException("fileName is missing"); 
         
        string arguments = stepConfig.TryGetProperty("arguments", out var argsProp) ? argsProp.GetString() ?? "" : ""; 
        string workDir = stepConfig.TryGetProperty("workDir", out var wdProp) ? wdProp.GetString() ?? AppContext.BaseDirectory : AppContext.BaseDirectory; 
        bool waitForExit = stepConfig.TryGetProperty("waitForExit", out var waitProp) ? waitProp.GetBoolean() : true; 

        // РРЅС‚РµСЂРїРѕР»СЏС†РёСЏ РїРµСЂРµРјРµРЅРЅС‹С…: Р·Р°РјРµРЅСЏРµРј {{key}} РЅР° Р·РЅР°С‡РµРЅРёСЏ РёР· Payload РєРѕРЅРІРµР№РµСЂР° 
        fileName = Interpolate(fileName, context); 
        arguments = Interpolate(arguments, context); 
        workDir = Interpolate(workDir, context); 

        context.Reporter.ReportState($"[ExecuteCommand] waitForExit={waitForExit}, fileName={fileName}, workDir={workDir}");

        // Fail fast if placeholders remain unresolved вЂ” prevents cryptic "file not found" from Process.Start
        var unresolved = new List<string>();
        if (fileName.Contains("{{")) unresolved.Add($"fileName='{fileName}'");
        if (arguments.Contains("{{")) unresolved.Add($"arguments='{arguments}'");
        if (workDir.Contains("{{")) unresolved.Add($"workDir='{workDir}'");
        if (unresolved.Count > 0)
        {
            return new CommandResult(false,
                $"OS.ExecuteCommand cannot launch: unresolved placeholders in {string.Join(", ", unresolved)}. " +
                "Upstream step (e.g. Game.LaunchCommand) did not set required context keys.");
        }

        // Skip real process launch in dry-run / test mode per INVARIANT_THEORY.md В§1.2
        if (stepConfig.TryGetProperty("dryRun", out var dr) && dr.GetBoolean())
        {
            context.Reporter.ReportState($"[DRY RUN] Would execute: {Path.GetFileName(fileName)} {arguments} (workDir: {workDir})");
            return new CommandResult(true);
        }
 
        if (!File.Exists(fileName) && !IsSystemCommand(fileName))
        {
            return new CommandResult(false, $"РСЃРїРѕР»РЅСЏРµРјС‹Р№ С„Р°Р№Р» РЅРµ РЅР°Р№РґРµРЅ: {fileName}");
        } 
 
        context.Reporter.ReportState($"Р—Р°РїСѓСЃРє: {Path.GetFileName(fileName)}..."); 
 
        var startInfo = new ProcessStartInfo 
        { 
            FileName = fileName, 
            Arguments = arguments, 
            WorkingDirectory = workDir, 
            UseShellExecute = false, 
            CreateNoWindow = true 
        }; 

        // Always redirect stderr so we can capture crash diagnostics regardless of waitForExit
        startInfo.RedirectStandardError = true;

        if (waitForExit)
        {
            startInfo.RedirectStandardOutput = true;
        }

        var process = new Process { StartInfo = startInfo };

        var stderrBuilder = new System.Text.StringBuilder();
        process.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                context.Reporter.ReportState($"[ERR] {e.Data}");
                stderrBuilder.AppendLine(e.Data);
            }
        };

        if (waitForExit)
        {
            // РџРµСЂРµРЅР°РїСЂР°РІР»СЏРµРј stdout РїСЂРѕС†РµСЃСЃР° РІ РЅР°С€ UI С‡РµСЂРµР· Reporter
            process.OutputDataReceived += (sender, e) => {
                if (!string.IsNullOrWhiteSpace(e.Data)) context.Reporter.ReportState($"[OUT] {e.Data}");
            };
        }
 
        try 
        { 
            process.Start();
            process.BeginErrorReadLine();

            if (waitForExit)
            {
                process.BeginOutputReadLine();
                await process.WaitForExitAsync(effectiveToken);

                if (process.ExitCode != 0)
                {
                    var stderr = stderrBuilder.ToString().Trim();
                    var details = string.IsNullOrEmpty(stderr)
                        ? $""
                        : $"\nStderr:\n{stderr}";
                    return new CommandResult(false, $"РџСЂРѕС†РµСЃСЃ {Path.GetFileName(fileName)} Р·Р°РІРµСЂС€РёР»СЃСЏ СЃ РѕС€РёР±РєРѕР№ (ExitCode: {process.ExitCode}){details}");
                }
            }
            else
            {
                // Fire-and-forget: register handle so pipeline can terminate child on cancellation.
                context.Set("os.running_process", new ProcessHandle(process));

                // Give process a brief grace period to detect immediate crash.
                try
                {
                    await Task.Delay(2000, effectiveToken);
                }
                catch (OperationCanceledException)
                {
                    // Pipeline shutting down: kill child process tree.
                    if (context.Get<IRunningProcessHandle>("os.running_process") is IRunningProcessHandle handle)
                    {
                        handle.Terminate();
                    }
                    return new CommandResult(false, "Launch cancelled by user.");
                }

                if (process.HasExited && process.ExitCode != 0)
                {
                    var stderr = stderrBuilder.ToString().Trim();
                    var fullCmd = $"{fileName} {arguments}";
                    var details = string.IsNullOrEmpty(stderr)
                        ? ""
                        : $"\nStderr:\n{stderr}";
                    return new CommandResult(false,
                        $"РџСЂРѕС†РµСЃСЃ РєСЂР°С€РЅСѓР»СЃСЏ РїСЂРё Р·Р°РїСѓСЃРєРµ (ExitCode: {process.ExitCode}). Command: {fullCmd} (workDir: {workDir}){details}");
                }
            } 
        } 
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CommandResult(false, $"РћС€РёР±РєР° Р·Р°РїСѓСЃРєР° РїСЂРѕС†РµСЃСЃР°: {ex.Message}");
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
        // РџСЂРѕСЃС‚Р°СЏ СЌРІСЂРёСЃС‚РёРєР° РґР»СЏ РїСЂРѕРїСѓСЃРєР° РїСЂРѕРІРµСЂРєРё File.Exists РґР»СЏ СЃРёСЃС‚РµРјРЅС‹С… РєРѕРјР°РЅРґ РІСЂРѕРґРµ "java" РёР»Рё "cmd" 
        return !fileName.Contains('/') && !fileName.Contains('\\') && !fileName.EndsWith(".exe"); 
    } 
 /// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior
 
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
} 
