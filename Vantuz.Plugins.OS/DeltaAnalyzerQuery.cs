using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Query: РђРЅР°Р»РёР· РґРµР»СЊС‚С‹ РјРµР¶РґСѓ С‚РµРєСѓС‰РёРј Рё С†РµР»РµРІС‹Рј СЃРѕСЃС‚РѕСЏРЅРёРµРј.
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ С‡С‚РµРЅРёРµ, РЅРµС‚ side effects.
/// F_doc: {delta analysis returns false positives for moved files or hash collisions}
/// E_doc: Unit test with staged file tree comparing expected vs actual delta
/// </summary>
public class DeltaAnalyzerQuery : IQueryPlugin
{
    public string Name => "OS.DeltaAnalyzerQuery";

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        // РџРђРўРўР•Р Рќ GRACEFUL SKIP
        var targetState = context.Get<List<FileState>>("TargetState");

        // Check for modpack manifest result from Net.ModpackManifest
        var manifestResult = context.Get<ModpackManifestResult>("Net.ModpackManifest.Result");
        if (manifestResult != null)
        {
            targetState = manifestResult.Files;
            context.Reporter.ReportState($"РњРѕРґРїР°Рє {manifestResult.Version}: {targetState.Count} С„Р°Р№Р»РѕРІ РґР»СЏ СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё.");
        }

        if (targetState == null || targetState.Count == 0)
        {
            context.Reporter.ReportState("РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ РєР°СЃС‚РѕРјРЅС‹С… С„Р°Р№Р»РѕРІ РЅРµ С‚СЂРµР±СѓРµС‚СЃСЏ.");
            return new DeltaAnalyzerResult(new List<FileState>(), new List<string>(), new List<MoveOperation>());
        }

        var purgeZones = context.Get<List<string>>("PurgeZones") ?? new List<string>();
        string mcDir = context.Get<string>("mcDir") ?? throw new InvalidOperationException("mcDir is missing in context");

        context.Reporter.ReportState("РђРЅР°Р»РёР· РёР·РјРµРЅРµРЅРёР№ Рё РґРµРґСѓРїР»РёРєР°С†РёСЏ...");

        return await Task.Run(() =>
        {
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

            // 1. РџСЂРѕРІРµСЂРєР° Р»РѕРєР°Р»СЊРЅС‹С… С„Р°Р№Р»РѕРІ
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

            // 2. РЎР±РѕСЂ С„Р°Р№Р»РѕРІ РЅР° СѓРґР°Р»РµРЅРёРµ РІ Р·РѕРЅР°С… РѕС‡РёСЃС‚РєРё
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

            // 3. Р”РµРґСѓРїР»РёРєР°С†РёСЏ (Local Move Optimization)
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
                            // РќР°Р№РґРµРЅРѕ СЃРѕРІРїР°РґРµРЅРёРµ! РњРѕР¶РЅРѕ РїСЂРѕСЃС‚Рѕ РїРµСЂРµРЅРµСЃС‚Рё С„Р°Р№Р» РІРјРµСЃС‚Рѕ СЃРєР°С‡РёРІР°РЅРёСЏ.
                            string destPath = PathHelper.GetSafePath(mcDir, downloadItem.RelativePath);
                            localMoveQueue.Add(new MoveOperation(deletePath, destPath));

                            downloadQueue.Remove(downloadItem);
                            deleteQueue.Remove(deletePath);
                            break;
                        }
                    }
                }
            }

            context.Reporter.ReportState($"РђРЅР°Р»РёР· Р·Р°РІРµСЂС€РµРЅ: {downloadQueue.Count} Рє Р·Р°РіСЂСѓР·РєРµ, {localMoveQueue.Count} Р»РѕРєР°Р»СЊРЅС‹С… РїРµСЂРµРјРµС‰РµРЅРёР№, {deleteQueue.Count} Рє СѓРґР°Р»РµРЅРёСЋ.");

            return new DeltaAnalyzerResult(downloadQueue, deleteQueue, localMoveQueue);
        });
    }
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Р РµР·СѓР»СЊС‚Р°С‚ Р°РЅР°Р»РёР·Р° РґРµР»СЊС‚С‹ РґР»СЏ РїРµСЂРµРґР°С‡Рё С‡РµСЂРµР· РјСѓС‚Р°С†РёРё.
/// </summary>
/// F_doc: {DeltaAnalyzerResult returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DeltaAnalyzerResult behavior
public record DeltaAnalyzerResult(
    List<FileState> DownloadQueue,
    List<string> DeleteQueue,
    List<MoveOperation> LocalMoveQueue
);
