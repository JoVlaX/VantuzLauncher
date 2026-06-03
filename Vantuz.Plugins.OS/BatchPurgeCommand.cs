using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Command: Пакетная очистка файлов и пустых директорий.
/// Per Armatura:76-78 - только запись/модификация состояния (удаление).
/// </summary>
public class BatchPurgeCommand : ICommandPlugin
{
    public string Name => "OS.BatchPurgeCommand";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        var deleteQueue = context.Get<List<string>>("DeleteQueue");
        var purgeZones = context.Get<List<string>>("PurgeZones");

        if ((deleteQueue == null || deleteQueue.Count == 0) && (purgeZones == null || purgeZones.Count == 0))
        {
            return new CommandResult(true);
        }

        string mcDir = context.Get<string>("mcDir") ?? throw new InvalidOperationException("mcDir is missing in context");

        context.Reporter.ReportState("Сборка мусора и очистка...");

        await Task.Run(() =>
        {
            // 1. Удаление файлов
            if (deleteQueue != null)
            {
                foreach (var filePath in deleteQueue)
                {
                    try
                    {
                        if (File.Exists(filePath)) File.Delete(filePath);
                    }
                    catch (IOException)
                    {
                        // Игнорируем заблокированные файлы
                    }
                }
            }

            // 2. Удаление пустых папок (Bottom-Up)
            if (purgeZones != null)
            {
                foreach (var zone in purgeZones)
                {
                    try
                    {
                        string zonePath = PathHelper.GetSafePath(mcDir, zone);
                        if (Directory.Exists(zonePath))
                        {
                            DeleteEmptyDirs(zonePath);
                        }
                    }
                    catch (IOException)
                    {
                        // Игнорируем ошибки доступа к папкам
                    }
                }
            }
        });

        int deletedFiles = deleteQueue?.Count ?? 0;
        context.Set("BatchPurgeDeletedFiles", deletedFiles);
        return new CommandResult(true);
    }

    private static void DeleteEmptyDirs(string startLocation)
    {
        foreach (var directory in Directory.GetDirectories(startLocation))
        {
            DeleteEmptyDirs(directory);
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                try { Directory.Delete(directory, false); } catch (IOException) { }
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
