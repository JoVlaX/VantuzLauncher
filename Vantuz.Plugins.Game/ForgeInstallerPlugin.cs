namespace Vantuz.Plugins.Game; 
  
 using System; 
 using System.Text.Json; 
 using System.Threading.Tasks; 
 using CmlLib.Core; 
 using CmlLib.Core.Installer.Forge; 
 using Vantuz.Core; 
  
 public class ForgeInstallerPlugin : IVantuzPlugin 
 { 
     public string Name => "Game.ForgeInstaller"; 
  
     public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
     { 
         string mcVersion = stepConfig.GetProperty("mcVersion").GetString() ?? throw new Exception("mcVersion is missing in config"); 
         string forgeVersion = stepConfig.GetProperty("forgeVersion").GetString() ?? throw new Exception("forgeVersion is missing in config"); 
  
         mcVersion = Interpolate(mcVersion, context); 
         forgeVersion = Interpolate(forgeVersion, context); 
  
         string mcDir = context.Get<string>("mcDir") ?? throw new Exception("mcDir is missing in Payload"); 
  
         context.Reporter.ReportState($"Установка Forge {forgeVersion} для Minecraft {mcVersion}..."); 
  
         var path = new MinecraftPath(mcDir); 
         var launcher = new MinecraftLauncher(path); 
         var forge = new MForge(launcher); 
  
         forge.FileProgressChanged += (sender, args) => 
         { 
             context.Reporter.ReportProgress("Установка Forge...", (double)args.ProgressedTasks / args.TotalTasks * 100); 
         }; 
  
         try 
         { 
             string resultingVersionName = await forge.Install(mcVersion, forgeVersion); 
             // Передаем эстафету следующему лезвию через Payload 
             context.Set("targetVersionName", resultingVersionName); 
             context.Reporter.ReportState($"Forge установлен: {resultingVersionName}"); 
         } 
         catch (Exception ex) 
         { 
             context.Abort($"Ошибка установки Forge: {ex.Message}"); 
             return; 
         } 
  
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
