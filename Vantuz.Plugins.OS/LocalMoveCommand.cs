using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Command: Р›РѕРєР°Р»СЊРЅРѕРµ РїРµСЂРµРјРµС‰РµРЅРёРµ С„Р°Р№Р»РѕРІ (РґРµРґСѓРїР»РёРєР°С†РёСЏ).
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ Р·Р°РїРёСЃСЊ/РјРѕРґРёС„РёРєР°С†РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ.
/// F_doc: {source file missing or destination already exists with different hash}
/// E_doc: Unit test verifying atomic move and rollback on failure
/// </summary>
public class LocalMoveCommand : ICommandPlugin
{
    public string Name => "OS.LocalMoveCommand";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        var localMoveQueue = context.Get<List<MoveOperation>>("LocalMoveQueue");
        if (localMoveQueue == null || localMoveQueue.Count == 0)
        {
            return new CommandResult(true);
        }

        context.Reporter.ReportState($"Р›РѕРєР°Р»СЊРЅРѕРµ РїРµСЂРµРјРµС‰РµРЅРёРµ С„Р°Р№Р»РѕРІ ({localMoveQueue.Count})...");

        int successCount = await Task.Run(() =>
        {
            int count = 0;
            foreach (var op in localMoveQueue)
            {
                try
                {
                    if (File.Exists(op.SourcePath))
                    {
                        // PathHelper.GetSafePath РІ DeltaAnalyzer СѓР¶Рµ РіР°СЂР°РЅС‚РёСЂСѓРµС‚ СЃСѓС‰РµСЃС‚РІРѕРІР°РЅРёРµ РїР°РїРєРё РЅР°Р·РЅР°С‡РµРЅРёСЏ
                        if (File.Exists(op.DestPath)) File.Delete(op.DestPath);
                        File.Move(op.SourcePath, op.DestPath);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    context.Reporter.ReportState($"[WARN] РќРµ СѓРґР°Р»РѕСЃСЊ РїРµСЂРµРјРµСЃС‚РёС‚СЊ {Path.GetFileName(op.SourcePath)}: {ex.Message}");
                    // РќРµ РїСЂРµСЂС‹РІР°РµРј РІРµСЃСЊ РїСЂРѕС†РµСЃСЃ РёР·-Р·Р° РѕРґРЅРѕР№ РѕС€РёР±РєРё РїРµСЂРµРјРµС‰РµРЅРёСЏ, С„Р°Р№Р» РїСЂРѕСЃС‚Рѕ РїРѕРїР°РґРµС‚ РІ РѕС‡РµСЂРµРґСЊ Р·Р°РіСЂСѓР·РєРё РІ СЃР»РµРґСѓСЋС‰РёР№ СЂР°Р·
                }
            }
            return count;
        });

        context.Set("LocalMoveSuccessCount", successCount);
        return new CommandResult(true);
    }
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
