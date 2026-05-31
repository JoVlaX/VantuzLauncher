using System; 
using System.IO; 
using System.IO.Compression; 
using System.Net.Http; 
using System.Text.Json; 
using System.Threading.Tasks; 
using Vantuz.Core; 
 
namespace Vantuz.Plugins.Net 
{ 
    /// <summary>
    /// ARM005 CQRS Command: Скачивание и подготовка обновлений лаунчера.
    /// Per Armatura:76-78 - только запись/модификация состояния.
    /// </summary>
    public class UpdateCommand : ICommandPlugin 
    { 
        public string Name => "Net.UpdateCommand"; 
        private readonly HttpClient _httpClient; 
 
        public UpdateCommand() 
        { 
            _httpClient = new HttpClient(); 
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-UpdateCommand/2.0"); 
            // Per INVARIANT_THEORY.md §4.3 - timeout for HostManaged resource (HTTP download)
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        } 
 
        public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig) 
        { 
            // Per INVARIANT_THEORY.md §498 Explicitness - check for .dev marker file
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            bool isDevMode = File.Exists(Path.Combine(baseDir, ".dev"));
            if (isDevMode)
            {
                context.Reporter.ReportState("[DEV MODE] Update check skipped per .dev marker file");
                return new CommandResult(true);
            }

            // Per INVARIANT_THEORY.md §498 - explicit config flag also supported
            bool skipUpdate = stepConfig.TryGetProperty("_skipUpdate", out var skipProp) && skipProp.GetBoolean();
            if (skipUpdate)
            {
                context.Reporter.ReportState("[DEV MODE] Update check skipped per _skipUpdate config");
                return new CommandResult(true);
            }

            string currentVer = stepConfig.TryGetProperty("currentVersion", out var cv) ? Interpolate(cv.GetString() ?? "", context) : ""; 
            string targetVer = stepConfig.TryGetProperty("targetVersion", out var tv) ? Interpolate(tv.GetString() ?? "", context) : ""; 

            if (!string.IsNullOrEmpty(currentVer) && currentVer == targetVer)
            {
                context.Reporter.ReportState("Установлена актуальная версия.");
                return new CommandResult(true);
            } 

            string url = stepConfig.GetProperty("url").GetString()
                ?? throw new InvalidOperationException("URL is missing in UpdateCommand"); 
            url = Interpolate(url, context); 
 
            // baseDir already defined at method start for dev mode check
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

                return new CommandResult(true);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"Сбой подготовки обновления: {ex.Message}");
            }
        } 
 
        private static string Interpolate(string text, CommandContext context) 
        { 
            if (string.IsNullOrEmpty(text)) return text; 
            var mutations = context.GetMutations();
            foreach (var kvp in mutations) 
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
