using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Query: Анализ дельты между текущим и целевым состоянием.
/// Per .traerules:76-78 - только чтение, нет side effects.
/// </summary>
public class DeltaAnalyzerQuery : IQueryPlugin
{
    public string Name => "OS.DeltaAnalyzer";

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        // ПАТТЕРН GRACEFUL SKIP 
        var targetState = context.Get<List<FileState>>("TargetState");
        
        // Check for modpack manifest result from Net.ModpackManifest
        var manifestResult = context.Get<ModpackManifestResult>("Net.ModpackManifest.Result");
        if (manifestResult != null)
        {
            targetState = manifestResult.Files;
            context.Reporter.ReportState($"Модпак {manifestResult.Version}: {targetState.Count} файлов для синхронизации.");
        }
        
        if (targetState == null || targetState.Count == 0)
        {
            context.Reporter.ReportState("Синхронизация кастомных файлов не требуется.");
            return new DeltaAnalyzerResult(new List<FileState>(), new List<string>(), new List<MoveOperation>());
        }

        var purgeZones = context.Get<List<string>>("PurgeZones") ?? new List<string>();
        string mcDir = context.Get<string>("mcDir") ?? throw new InvalidOperationException("mcDir is missing in context");
        
        context.Reporter.ReportState("Анализ изменений и дедупликация...");

        var downloadQueue = new List<FileState>();
        var deleteQueue = new List<string>();
        var localMoveQueue = new List<MoveOperation>();
        
        // Handle removed files from modpack manifest
        if (manifestResult?.RemovedFiles != null && manifestResult.RemovedFiles.Count > 0)
        {
            foreach (var removedFile in manifestResult.RemovedFiles)
            {
                string fullPath = PathHelper.GetSafePath(mcDir, removedFile);
                if (File.Exists(fullPath))
                {
                    deleteQueue.Add(fullPath);
                }
            }
        }

        // 1. Проверка локальных файлов
        foreach (var file in targetState)
        {
            string fullPath = PathHelper.GetSafePath(mcDir, file.RelativePath);
            bool needsUpdate = true;

            if (File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                if (info.Length == file.Size)
                {
                    string localHash = PathHelper.CalculateHash(fullPath);
                    if (localHash == file.Hash)
                    {
                        needsUpdate = false;
                    }
                }
            }

            if (needsUpdate)
            {
                downloadQueue.Add(file);
            }
        }

        // 2. Сбор файлов на удаление в зонах очистки
        foreach (var zone in purgeZones)
        {
            string zonePath = PathHelper.GetSafePath(mcDir, zone);
            if (Directory.Exists(zonePath))
            {
                var filesInZone = Directory.GetFiles(zonePath, "*", SearchOption.AllDirectories);
                foreach (var filePath in filesInZone)
                {
                    string relativePath = Path.GetRelativePath(mcDir, filePath);
                    if (!targetState.Any(ts => ts.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        deleteQueue.Add(filePath);
                    }
                }
            }
        }

        // 3. Дедупликация (Local Move Optimization)
        var toDownload = new List<FileState>(downloadQueue);
        var toDelete = new List<string>(deleteQueue);

        foreach (var downloadItem in toDownload.ToList())
        {
            foreach (var deletePath in toDelete.ToList())
            {
                var deleteInfo = new FileInfo(deletePath);
                if (deleteInfo.Length == downloadItem.Size)
                {
                    string deleteHash = PathHelper.CalculateHash(deletePath);
                    if (deleteHash == downloadItem.Hash)
                    {
                        // Найдено совпадение! Можно просто перенести файл вместо скачивания.
                        string destPath = PathHelper.GetSafePath(mcDir, downloadItem.RelativePath);
                        localMoveQueue.Add(new MoveOperation(deletePath, destPath));
                        
                        downloadQueue.Remove(downloadItem);
                        deleteQueue.Remove(deletePath);
                        break;
                    }
                }
            }
        }

        context.Reporter.ReportState($"Анализ завершен: {downloadQueue.Count} к загрузке, {localMoveQueue.Count} локальных перемещений, {deleteQueue.Count} к удалению.");

        return new DeltaAnalyzerResult(downloadQueue, deleteQueue, localMoveQueue);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Результат анализа дельты для передачи через мутации.
/// </summary>
public record DeltaAnalyzerResult(
    List<FileState> DownloadQueue,
    List<string> DeleteQueue,
    List<MoveOperation> LocalMoveQueue
);
