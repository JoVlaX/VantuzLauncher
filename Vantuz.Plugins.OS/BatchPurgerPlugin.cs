using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

public class BatchPurgerPlugin : IVantuzPlugin
{
    public string Name => "OS.BatchPurger";

    public async Task InvokeAsync(Vantuz.Core.ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next)
    {
        var deleteQueue = context.Get<List<string>>("DeleteQueue");
        var purgeZones = context.Get<List<string>>("PurgeZones");
        
        if ((deleteQueue == null || deleteQueue.Count == 0) && (purgeZones == null || purgeZones.Count == 0))
        {
            await next(context);
            return;
        }

        string mcDir = context.Get<string>("mcDir") ?? throw new Exception("mcDir is missing in context");

        context.Reporter.ReportState("Сборка мусора и очистка...");

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

        await next(context);
    }

    private void DeleteEmptyDirs(string startLocation)
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
