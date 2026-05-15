using System; 
using System.IO; 
using System.IO.Compression; 
using System.Net.Http; 
using System.Text.Json; 
using System.Threading.Tasks; 
using Vantuz.Core; 
 
namespace Vantuz.Plugins.Net 
{ 
    public class UpdaterPlugin : IVantuzPlugin 
    { 
        public string Name => "Net.Updater"; 
        private readonly HttpClient _httpClient; 
 
        public UpdaterPlugin() 
        { 
            _httpClient = new HttpClient(); 
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-Updater/2.0"); 
        } 
 
        public async Task InvokeAsync(Vantuz.Core.ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
        { 
            string currentVer = stepConfig.TryGetProperty("currentVersion", out var cv) ? Interpolate(cv.GetString() ?? "", context) : ""; 
            string targetVer = stepConfig.TryGetProperty("targetVersion", out var tv) ? Interpolate(tv.GetString() ?? "", context) : ""; 

            if (!string.IsNullOrEmpty(currentVer) && currentVer == targetVer) 
            { 
                context.Reporter.ReportState("Установлена актуальная версия."); 
                await next(context); 
                return; 
            } 

            string url = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing in Updater"); 
            url = Interpolate(url, context); 
 
            string baseDir = AppDomain.CurrentDomain.BaseDirectory; 
            string pendingDir = Path.Combine(baseDir, ".update_pending"); 
            string tempZip = Path.Combine(baseDir, "update_temp.zip"); 
 
            try 
            { 
                context.Reporter.ReportState("Скачивание обновления лаунчера..."); 
                 
                // 1. Скачивание (Staging) 
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)) 
                { 
                    response.EnsureSuccessStatusCode(); 
                    using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None); 
                    await response.Content.CopyToAsync(fs, context.CancellationToken); 
                } 
 
                context.Reporter.ReportState("Распаковка обновления..."); 
                 
                // 2. Очистка старой песочницы и распаковка 
                if (Directory.Exists(pendingDir)) Directory.Delete(pendingDir, true); 
                Directory.CreateDirectory(pendingDir); 
                ZipFile.ExtractToDirectory(tempZip, pendingDir, overwriteFiles: true); 
                File.Delete(tempZip); 
 
                // 3. Поиск скрипта обновления в распакованном архиве 
                string scriptName = stepConfig.TryGetProperty("scriptName", out var sn) ? sn.GetString()! : "update.bat"; 
                string scriptPath = Path.Combine(pendingDir, scriptName); 
                
                if (File.Exists(scriptPath)) 
                { 
                    // 4. Сигнализируем Ядру о необходимости перезапуска 
                    context.Set("UpdateReady", true); 
                    context.Set("UpdateScript", scriptPath); 
                    context.Reporter.ReportState("Обновление готово. Инициализация перезапуска..."); 
                } 
                else 
                { 
                    context.Reporter.ReportState("Обновление распаковано, но скрипт не найден."); 
                } 
            } 
            catch (Exception ex) 
            { 
                context.Abort($"Сбой подготовки обновления: {ex.Message}"); 
                return; 
            } 
 
            // Передаем управление дальше. Ядро само решит, когда остановиться. 
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
 
        public ValueTask DisposeAsync() 
        { 
            _httpClient.Dispose(); 
            return ValueTask.CompletedTask; 
        } 
    } 
} 
