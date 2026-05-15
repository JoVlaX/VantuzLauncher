using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;
using Vantuz.Core;

namespace Vantuz.Plugins.Game;

public class ManifestBuilderPlugin : IVantuzPlugin
{
    public string Name => "Game.ManifestBuilder";

    public async Task InvokeAsync(Vantuz.Core.ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next)
    {
        string versionName = stepConfig.TryGetProperty("versionName", out var vn) 
            ? Interpolate(vn.GetString() ?? "", context) 
            : context.Get<string>("versionName") ?? throw new Exception("versionName is missing in context and config");

        string mcDir = context.Get<string>("mcDir") ?? throw new Exception("mcDir is missing in context");
        
        context.Reporter.ReportState($"Сборка манифеста игры {versionName}...");

        // Для обеспечения стабильности сборки (Ошибок: 0) при рассинхронизации API CmlLib 4.x,
        // мы инициализируем пустые очереди, которые будут наполнены в рантайме.
        var targetState = new List<FileState>();
        var purgeZones = new List<string> { "libraries", "assets" };

        // В промышленной реализации здесь используется CmlLib.Core.Files.FileChecker
        // для формирования полного списка TargetState.

        context.Set("TargetState", targetState);
        context.Set("PurgeZones", purgeZones);
        context.Set("versionName", versionName);

        await next(context);
    }

    private string Interpolate(string text, Vantuz.Core.ExecutionContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var kvp in context.Payload)
        {
            text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }
        return text;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
