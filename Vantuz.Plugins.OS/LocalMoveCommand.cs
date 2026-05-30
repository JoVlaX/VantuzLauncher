using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

/// <summary>
/// ARM005 CQRS Command: Локальное перемещение файлов (дедупликация).
/// Per .traerules:76-78 - только запись/модификация состояния.
/// </summary>
public class LocalMoveCommand : ICommandPlugin
{
    public string Name => "OS.LocalMove";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        var localMoveQueue = context.Get<List<MoveOperation>>("LocalMoveQueue");
        if (localMoveQueue == null || localMoveQueue.Count == 0)
        {
            return new CommandResult(true);
        }

        context.Reporter.ReportState($"Локальное перемещение файлов ({localMoveQueue.Count})...");

        int successCount = await Task.Run(() =>
        {
            int count = 0;
            foreach (var op in localMoveQueue)
            {
                try
                {
                    if (File.Exists(op.SourcePath))
                    {
                        // PathHelper.GetSafePath в DeltaAnalyzer уже гарантирует существование папки назначения
                        if (File.Exists(op.DestPath)) File.Delete(op.DestPath);
                        File.Move(op.SourcePath, op.DestPath);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    context.Reporter.ReportState($"[WARN] Не удалось переместить {Path.GetFileName(op.SourcePath)}: {ex.Message}");
                    // Не прерываем весь процесс из-за одной ошибки перемещения, файл просто попадет в очередь загрузки в следующий раз
                }
            }
            return count;
        });

        context.Set("LocalMoveSuccessCount", successCount);
        return new CommandResult(true);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
