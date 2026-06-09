using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Command: РџР°РєРµС‚РЅР°СЏ РѕС‡РёСЃС‚РєР° С„Р°Р№Р»РѕРІ Рё РїСѓСЃС‚С‹С… РґРёСЂРµРєС‚РѕСЂРёР№.
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ Р·Р°РїРёСЃСЊ/РјРѕРґРёС„РёРєР°С†РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ (СѓРґР°Р»РµРЅРёРµ).
/// F_doc: {purge target contains non-empty directory or file not matching pattern}
/// E_doc: Unit test with mock file system verifying purge scope
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

        context.Reporter.ReportState("РЎР±РѕСЂРєР° РјСѓСЃРѕСЂР° Рё РѕС‡РёСЃС‚РєР°...");

        await Task.Run(() =>
        {
            // 1. РЈРґР°Р»РµРЅРёРµ С„Р°Р№Р»РѕРІ
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
                        // РРіРЅРѕСЂРёСЂСѓРµРј Р·Р°Р±Р»РѕРєРёСЂРѕРІР°РЅРЅС‹Рµ С„Р°Р№Р»С‹
                    }
                }
            }

            // 2. РЈРґР°Р»РµРЅРёРµ РїСѓСЃС‚С‹С… РїР°РїРѕРє (Bottom-Up)
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
                        // РРіРЅРѕСЂРёСЂСѓРµРј РѕС€РёР±РєРё РґРѕСЃС‚СѓРїР° Рє РїР°РїРєР°Рј
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
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
