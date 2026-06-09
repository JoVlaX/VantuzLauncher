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
/// ARM005 CQRS Command: РџР°РєРµС‚РЅР°СЏ Р·Р°РіСЂСѓР·РєР° С„Р°Р№Р»РѕРІ СЃ С‚СЂР°РЅР·Р°РєС†РёРѕРЅРЅС‹Рј РєРѕРјРјРёС‚РѕРј.
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ Р·Р°РїРёСЃСЊ/РјРѕРґРёС„РёРєР°С†РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ.
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

        context.Reporter.ReportState($"Р—Р°РіСЂСѓР·РєР° С„Р°Р№Р»РѕРІ ({downloadQueue.Count})...");

        var successfulDownloads = new List<(string finalPath, string tmpPath, string backupPath)>();
        int completedCount = 0;

        try
        {
            // Р¤Р°Р·Р° 1: РЎРєР°С‡РёРІР°РЅРёРµ РІРѕ РІСЂРµРјРµРЅРЅС‹Рµ С„Р°Р№Р»С‹
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

                        // Р’РµСЂРёС„РёРєР°С†РёСЏ С…СЌС€Р° (skip if no expected hash provided)
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
                            context.Reporter.ReportProgress("Р—Р°РіСЂСѓР·РєР°",
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

            // Р¤Р°Р·Р° 2: РўРµРЅРµРІРѕР№ РєРѕРјРјРёС‚ (Transactionally Safe)
            context.Reporter.ReportState("РџСЂРёРјРµРЅРµРЅРёРµ РѕР±РЅРѕРІР»РµРЅРёР№...");
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

                // РЈСЃРїРµС… - СѓРґР°Р»СЏРµРј Р±СЌРєР°РїС‹
                foreach (var item in committedFiles)
                {
                    try { if (File.Exists(item.backupPath)) File.Delete(item.backupPath); } catch (Exception ex) { Console.WriteLine($"[DownloadCommand] WARN: failed to delete backup {item.backupPath}: {ex.Message}"); }
                }
            }
            catch (IOException ex)
            {
                // РћС‚РєР°С‚ РїСЂРё РѕС€РёР±РєРµ I/O
                context.Reporter.ReportState($"[CRITICAL] РћС€РёР±РєР° I/O РїСЂРё РєРѕРјРјРёС‚Рµ: {ex.Message}. РќР°С‡РёРЅР°СЋ РѕС‚РєР°С‚...");
                foreach (var item in committedFiles)
                {
                    try
                    {
                        if (File.Exists(item.finalPath)) File.Delete(item.finalPath);
                        if (File.Exists(item.backupPath)) File.Move(item.backupPath, item.finalPath);
                    }
                    catch (Exception rollbackEx) { Console.WriteLine($"[DownloadCommand] WARN: rollback failed for {item.finalPath}: {rollbackEx.Message}"); }
                }
                return new CommandResult(false, "РћС€РёР±РєР° I/O Р±Р»РѕРєРёСЂРѕРІРєРё, СЃРѕСЃС‚РѕСЏРЅРёРµ РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРѕ");
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
            // РћС‡РёСЃС‚РєР° РІСЂРµРјРµРЅРЅС‹С… С„Р°Р№Р»РѕРІ РїСЂРё Р»СЋР±РѕР№ РѕС€РёР±РєРµ
            foreach (var item in successfulDownloads)
            {
                try { if (File.Exists(item.tmpPath)) File.Delete(item.tmpPath); } catch (Exception innerEx) { Console.WriteLine($"[DownloadCommand] WARN: failed to delete temp {item.tmpPath}: {innerEx.Message}"); }
            }
            return new CommandResult(false, $"РћС€РёР±РєР° РїСЂРё Р·Р°РіСЂСѓР·РєРµ: {ex.Message}");
        }
    }
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }
}

#pragma warning restore ARM010

