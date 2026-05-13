namespace Vantuz.Plugins.Game; 
 
using System; 
using System.Text.Json; 
using System.Threading.Tasks; 
using CmlLib.Core; 
using CmlLib.Core.Auth; 
using CmlLib.Core.ProcessBuilder; 
using Vantuz.Core; 
 
public class CmlLaunchPlugin : IVantuzPlugin 
{ 
    public string Name => "Game.CmlLaunch"; 
 
    public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
    { 
        // 1. Локальные параметры шага 
        string versionName = stepConfig.GetProperty("versionName").GetString() ?? throw new Exception("versionName is missing"); 
        string? authlibPath = stepConfig.TryGetProperty("authlibPath", out var alp) ? alp.GetString() : null; 
 
        // 2. Глобальные параметры из Payload 
        string mcDir = context.Get<string>("mcDir") ?? throw new Exception("mcDir is missing in Payload"); 
        string javaPath = context.Get<string>("javaPath") ?? "java"; 
        int ramMb = context.Get<int>("ramMb"); 
        if (ramMb == 0) ramMb = 4096; 
 
        string accessToken = context.Get<string>("accessToken") ?? ""; 
        string uuid = context.Get<string>("uuid") ?? ""; 
        string playerName = context.Get<string>("playerName") ?? "Player"; 
 
        // Интерполяция переменных 
        versionName = Interpolate(versionName, context); 
        if (authlibPath != null) authlibPath = Interpolate(authlibPath, context); 
 
        context.Reporter.ReportState($"Подготовка файлов игры ({versionName})..."); 
 
        var path = new MinecraftPath(mcDir); 
        var launcher = new MinecraftLauncher(path); 
 
        // Перехват прогресса скачивания библиотек CmlLib 
        launcher.FileProgressChanged += (sender, args) => 
        { 
            context.Reporter.ReportProgress(args.Name, (double)args.ProgressedTasks / args.TotalTasks * 100); 
        }; 
 
        // Загрузка метаданных и недостающих файлов 
        var version = await launcher.GetVersionAsync(versionName); 
        await launcher.InstallAsync(version); 
 
        context.Reporter.ReportState("Генерация аргументов запуска..."); 
 
        var session = new MSession 
        { 
            Username = playerName, 
            UUID = uuid, 
            AccessToken = accessToken, 
            UserType = "mojang" 
        }; 
 
        var launchOption = new MLaunchOption 
        { 
            Session = session, 
            MaximumRamMb = ramMb, 
            JavaPath = javaPath 
        }; 
 
        // Инъекция Yggdrasil Authlib (если указан путь) 
        if (!string.IsNullOrEmpty(authlibPath)) 
        { 
            launchOption.ExtraJvmArguments = new[] 
            { 
                new MArgument($"-javaagent:{authlibPath}=https://troglobit.webhm.pro/yggdrasil/") 
            }; 
        } 
 
        // CmlLib собирает все аргументы в готовый объект Process 
        var process = await launcher.BuildProcessAsync(versionName, launchOption); 
 
        // 3. Payload-Driven магия: мы не запускаем игру сами! 
        // Мы кладем готовую команду и огромную строку аргументов в контекст 
        context.Set("gameCommand", process.StartInfo.FileName); 
        context.Set("gameArgs", process.StartInfo.Arguments); 
        context.Set("gameWorkDir", process.StartInfo.WorkingDirectory); 
 
        await next(context); 
    } 
 
    private string Interpolate(string text, ExecutionContext context) 
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
