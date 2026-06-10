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
    /// ARM005 CQRS Command: РЎРєР°С‡РёРІР°РЅРёРµ Рё РїРѕРґРіРѕС‚РѕРІРєР° РѕР±РЅРѕРІР»РµРЅРёР№ Р»Р°СѓРЅС‡РµСЂР°.
    /// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ Р·Р°РїРёСЃСЊ/РјРѕРґРёС„РёРєР°С†РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ.
    /// F_doc: {update archive hash mismatch or version not newer than current}
    /// E_doc: Unit test with mock HttpClient returning mismatched hash
    /// </summary>
    public class UpdateCommand : ICommandPlugin 
    { 
        public string Name => "Net.UpdateCommand"; 
        private readonly HttpClient _httpClient; 
 
        public UpdateCommand() 
        { 
            _httpClient = new HttpClient(); 
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-UpdateCommand/2.0"); 
        } 
 
        public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig) 
        { 
            string currentVer = stepConfig.TryGetProperty("currentVersion", out var cv) ? Interpolate(cv.GetString() ?? "", context) : ""; 
            string targetVer = stepConfig.TryGetProperty("targetVersion", out var tv) ? Interpolate(tv.GetString() ?? "", context) : ""; 

            if (!string.IsNullOrEmpty(currentVer) && currentVer == targetVer)
            {
                context.Reporter.ReportState("РЈСЃС‚Р°РЅРѕРІР»РµРЅР° Р°РєС‚СѓР°Р»СЊРЅР°СЏ РІРµСЂСЃРёСЏ.");
                return new CommandResult(true);
            } 

            if (!stepConfig.TryGetProperty("url", out var urlProp) || urlProp.GetString() is not { } url)
                throw new InvalidOperationException("URL is missing in UpdateCommand"); 
            url = Interpolate(url, context); 
 
            string baseDir = AppDomain.CurrentDomain.BaseDirectory; 
            string pendingDir = Path.Combine(baseDir, ".update_pending"); 
            string tempZip = Path.Combine(baseDir, "update_temp.zip"); 
 
            try 
            { 
                context.Reporter.ReportState("РЎРєР°С‡РёРІР°РЅРёРµ РѕР±РЅРѕРІР»РµРЅРёСЏ Р»Р°СѓРЅС‡РµСЂР°..."); 
                 
                // 1. РЎРєР°С‡РёРІР°РЅРёРµ (Staging) 
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)) 
                { 
                    response.EnsureSuccessStatusCode(); 
                    using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None); 
                    await response.Content.CopyToAsync(fs, context.CancellationToken); 
                } 
 
                context.Reporter.ReportState("Р Р°СЃРїР°РєРѕРІРєР° РѕР±РЅРѕРІР»РµРЅРёСЏ..."); 
                 
                // 2. РћС‡РёСЃС‚РєР° СЃС‚Р°СЂРѕР№ РїРµСЃРѕС‡РЅРёС†С‹ Рё СЂР°СЃРїР°РєРѕРІРєР° 
                if (Directory.Exists(pendingDir)) Directory.Delete(pendingDir, true); 
                Directory.CreateDirectory(pendingDir); 
                ZipFile.ExtractToDirectory(tempZip, pendingDir, overwriteFiles: true); 
                File.Delete(tempZip); 
 
                // 3. РџРѕРёСЃРє СЃРєСЂРёРїС‚Р° РѕР±РЅРѕРІР»РµРЅРёСЏ РІ СЂР°СЃРїР°РєРѕРІР°РЅРЅРѕРј Р°СЂС…РёРІРµ 
                string scriptName = stepConfig.TryGetProperty("scriptName", out var sn) ? sn.GetString()! : "update.bat"; 
                string scriptPath = Path.Combine(pendingDir, scriptName); 
                
                if (File.Exists(scriptPath)) 
                { 
                    // 4. РЎРёРіРЅР°Р»РёР·РёСЂСѓРµРј РЇРґСЂСѓ Рѕ РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё РїРµСЂРµР·Р°РїСѓСЃРєР° 
                    context.Set("UpdateReady", true); 
                    context.Set("UpdateScript", scriptPath); 
                    context.Reporter.ReportState("РћР±РЅРѕРІР»РµРЅРёРµ РіРѕС‚РѕРІРѕ. РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РїРµСЂРµР·Р°РїСѓСЃРєР°..."); 
                } 
                else
                {
                    context.Reporter.ReportState("РћР±РЅРѕРІР»РµРЅРёРµ СЂР°СЃРїР°РєРѕРІР°РЅРѕ, РЅРѕ СЃРєСЂРёРїС‚ РЅРµ РЅР°Р№РґРµРЅ.");
                }

                return new CommandResult(true);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"РЎР±РѕР№ РїРѕРґРіРѕС‚РѕРІРєРё РѕР±РЅРѕРІР»РµРЅРёСЏ: {ex.Message}");
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
 /// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior
 
        public ValueTask DisposeAsync() 
        { 
            _httpClient.Dispose(); 
            return ValueTask.CompletedTask; 
        } 
    } 
} 
