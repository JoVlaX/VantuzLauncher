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
    public string Name => "OS.Execute"; 
 
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
            RedirectStandardOutput = true, 
            RedirectStandardError = true, 
            CreateNoWindow = true 
        }; 
 
        using var process = new Process { StartInfo = startInfo }; 
 
        // Перенаправляем вывод процесса в наш UI через Reporter 
        process.OutputDataReceived += (sender, e) => { 
            if (!string.IsNullOrWhiteSpace(e.Data)) context.Reporter.ReportState($"[OUT] {e.Data}"); 
        }; 
        process.ErrorDataReceived += (sender, e) => { 
            if (!string.IsNullOrWhiteSpace(e.Data)) context.Reporter.ReportState($"[ERR] {e.Data}"); 
        }; 
 
        try 
        { 
            process.Start(); 
            process.BeginOutputReadLine(); 
            process.BeginErrorReadLine(); 
 
            if (waitForExit) 
            { 
                await process.WaitForExitAsync(context.CancellationToken); 
                 
                if (process.ExitCode != 0)
                {
                    return new CommandResult(false, $"Процесс {Path.GetFileName(fileName)} завершился с ошибкой (ExitCode: {process.ExitCode})");
                } 
            } 
            else 
            { 
                // Если мы не ждем завершения (например, запуск самой игры), 
                // просто даем процессу немного времени на старт перед тем, как отпустить конвейер 
                await Task.Delay(2000, context.CancellationToken); 
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
