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
            string url = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing in Updater"); 
            url = Interpolate(url, context); 
 
            string baseDir = AppDomain.CurrentDomain.BaseDirectory; 
            string pendingDir = Path.Combine(baseDir, ".update_pending"); 
            string batPath = Path.Combine(baseDir, "update.bat"); 
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
 
                // 3. Генерация внешнего загрузчика (External Bootstrapper) с абсолютной защитой путей 
                string batContent = "@echo off\n" + 
                                    "cd /d \"%~dp0\"\n" + // ГАРАНТИЯ работы в правильной директории 
                                    "timeout /t 2 /nobreak > NUL\n" + 
                                    "xcopy /Y /S /E \".update_pending\\*\" \".\"\n" + 
                                    "rmdir /S /Q \".update_pending\"\n" + 
                                    "start \"\" \"VantuzLauncher.exe\"\n" + 
                                    "del \"%~f0\""; 
                File.WriteAllText(batPath, batContent); 
 
                // 4. Сигнализируем Ядру о необходимости перезапуска 
                context.Set("UpdateReady", true); 
                context.Set("UpdateScript", batPath); 
                context.Reporter.ReportState("Обновление готово. Инициализация перезапуска..."); 
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
