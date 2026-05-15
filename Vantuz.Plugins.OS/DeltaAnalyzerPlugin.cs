using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

public class DeltaAnalyzerPlugin : IVantuzPlugin
{
    public string Name => "OS.DeltaAnalyzer";

    public async Task InvokeAsync(Vantuz.Core.ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next)
    {
        var targetState = context.Get<List<FileState>>("TargetState");
        var purgeZones = context.Get<List<string>>("PurgeZones") ?? new List<string>();
        string mcDir = context.Get<string>("mcDir") ?? throw new Exception("mcDir is missing in context");

        if (targetState == null || targetState.Count == 0)
        {
            context.Abort("TargetState is empty. Analysis aborted.");
            return;
        }

        context.Reporter.ReportState("Анализ изменений и дедупликация...");

        var downloadQueue = new List<FileState>();
        var deleteQueue = new List<string>();
        var localMoveQueue = new List<MoveOperation>();

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

        context.Set("DownloadQueue", downloadQueue);
        context.Set("DeleteQueue", deleteQueue);
        context.Set("LocalMoveQueue", localMoveQueue);

        context.Reporter.ReportState($"Анализ завершен: {downloadQueue.Count} к загрузке, {localMoveQueue.Count} локальных перемещений, {deleteQueue.Count} к удалению.");

        await next(context);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
