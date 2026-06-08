#pragma warning disable ARM010 // FileStream requires DAG host-managed disposal; TODO: refactor to DAG ref counting (deadline: 2026-12-01)

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.Net;

/// <summary>
/// ARM005 CQRS Command: Пакетная загрузка файлов с транзакционным коммитом.
/// Per Armatura:76-78 - только запись/модификация состояния.
/// </summary>
public class DownloadCommand : ICommandPlugin
{
    public string Name => "Net.DownloadCommand";
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(4);

    public DownloadCommand()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-DownloadCommand/2.0");
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        context.Reporter.ReportState($"[DEBUG] DownloadCommand config: {stepConfig}");
        var downloadQueue = context.Get<List<FileState>>("DownloadQueue");

        if (downloadQueue == null || downloadQueue.Count == 0)
        {
            // Fallback: explicit files array from stepConfig for one-off downloads
            if (stepConfig.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
            {
                downloadQueue = new List<FileState>();
                foreach (var fileEl in filesProp.EnumerateArray())
                {
                    var relativePath = fileEl.TryGetProperty("relativePath", out var rp) ? rp.GetString() ?? "" : "";
                    var url = fileEl.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    var hash = fileEl.TryGetProperty("hash", out var h) ? h.GetString() ?? "" : "";
                    var size = fileEl.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0L;

                    if (!string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(url))
                    {
                        downloadQueue.Add(new FileState(relativePath, hash, size, url));
                    }
                }
            }

            if (downloadQueue == null || downloadQueue.Count == 0)
            {
                context.Reporter.ReportState("[DEBUG] DownloadCommand: no files to download");
                return new CommandResult(true);
            }
        }

        string mcDir = context.Get<string>("mcDir")
            ?? throw new InvalidOperationException("mcDir is missing in context");

        context.Reporter.ReportState($"Загрузка файлов ({downloadQueue.Count})...");

        var successfulDownloads = new List<(string finalPath, string tmpPath, string backupPath)>();
        int completedCount = 0;

        try
        {
            // Фаза 1: Скачивание во временные файлы
            var tasks = new List<Task>();
            foreach (var file in downloadQueue)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _semaphore.WaitAsync(context.CancellationToken);
                    try
                    {
                        if (string.IsNullOrEmpty(file.Url))
                            throw new InvalidOperationException($"URL missing for {file.RelativePath}");

                        string finalPath = PathHelper.GetSafePath(mcDir, file.RelativePath);
                        string tmpPath = finalPath + ".tmp";
                        string backupPath = finalPath + ".backup";

                        using (var response = await _httpClient.GetAsync(
                            file.Url,
                            HttpCompletionOption.ResponseHeadersRead,
                            context.CancellationToken))
                        {
                            response.EnsureSuccessStatusCode();
                            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await response.Content.CopyToAsync(fs, context.CancellationToken);
                            }
                        }

                        // Верификация хэша (skip if no expected hash provided)
                        if (!string.IsNullOrEmpty(file.Hash))
                        {
                            string downloadedHash = PathHelper.CalculateHash(tmpPath);
                            if (downloadedHash != file.Hash)
                            {
                                throw new InvalidOperationException(
                                    $"Hash mismatch for {file.RelativePath}. Expected: {file.Hash}, Actual: {downloadedHash}");
                            }
                        }

                        lock (successfulDownloads)
                        {
                            successfulDownloads.Add((finalPath, tmpPath, backupPath));
                            completedCount++;
                            context.Reporter.ReportProgress("Загрузка",
                                (double)completedCount / downloadQueue.Count * 100);
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, context.CancellationToken));
            }

            await Task.WhenAll(tasks);

            // Фаза 2: Теневой коммит (Transactionally Safe)
            context.Reporter.ReportState("Применение обновлений...");
            var committedFiles = new List<(string finalPath, string tmpPath, string backupPath)>();

            try
            {
                foreach (var item in successfulDownloads)
                {
                    if (File.Exists(item.finalPath))
                    {
                        File.Move(item.finalPath, item.backupPath, true);
                    }
                    File.Move(item.tmpPath, item.finalPath);
                    committedFiles.Add(item);
                }

                // Успех - удаляем бэкапы
                foreach (var item in committedFiles)
                {
                    try { if (File.Exists(item.backupPath)) File.Delete(item.backupPath); } catch (Exception ex) { Console.WriteLine($"[DownloadCommand] WARN: failed to delete backup {item.backupPath}: {ex.Message}"); }
                }
            }
            catch (IOException ex)
            {
                // Откат при ошибке I/O
                context.Reporter.ReportState($"[CRITICAL] Ошибка I/O при коммите: {ex.Message}. Начинаю откат...");
                foreach (var item in committedFiles)
                {
                    try
                    {
                        if (File.Exists(item.finalPath)) File.Delete(item.finalPath);
                        if (File.Exists(item.backupPath)) File.Move(item.backupPath, item.finalPath);
                    }
                    catch (Exception rollbackEx) { Console.WriteLine($"[DownloadCommand] WARN: rollback failed for {item.finalPath}: {rollbackEx.Message}"); }
                }
                return new CommandResult(false, "Ошибка I/O блокировки, состояние восстановлено");
            }

            context.Set("DownloadSuccess", true);
            context.Set("DownloadedCount", successfulDownloads.Count);
            return new CommandResult(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Очистка временных файлов при любой ошибке
            foreach (var item in successfulDownloads)
            {
                try { if (File.Exists(item.tmpPath)) File.Delete(item.tmpPath); } catch (Exception innerEx) { Console.WriteLine($"[DownloadCommand] WARN: failed to delete temp {item.tmpPath}: {innerEx.Message}"); }
            }
            return new CommandResult(false, $"Ошибка при загрузке: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }
}

#pragma warning restore ARM010

