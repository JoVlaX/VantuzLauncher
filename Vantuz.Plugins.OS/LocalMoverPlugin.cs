using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.OS;

public class LocalMoverPlugin : IVantuzPlugin
{
    public string Name => "OS.LocalMover";

    public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next)
    {
        var localMoveQueue = context.Get<List<MoveOperation>>("LocalMoveQueue");
        if (localMoveQueue == null || localMoveQueue.Count == 0)
        {              
            await next(context);
            return;
        }

        context.Reporter.ReportState($"Локальное перемещение файлов ({localMoveQueue.Count})...");

        foreach (var op in localMoveQueue)
        {
            try
            {
                if (File.Exists(op.SourcePath))
                {
                    // PathHelper.GetSafePath в DeltaAnalyzer уже гарантирует существование папки назначения
                    if (File.Exists(op.DestPath)) File.Delete(op.DestPath);
                    File.Move(op.SourcePath, op.DestPath);
                }
            }
            catch (Exception ex)
            {
                context.Reporter.ReportState($"[WARN] Не удалось переместить {Path.GetFileName(op.SourcePath)}: {ex.Message}");
                // Не прерываем весь процесс из-за одной ошибки перемещения, файл просто попадет в очередь загрузки в следующий раз
            }
        }

        await next(context);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
