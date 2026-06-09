namespace Vantuz.Plugins.Auth;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core; 
  
public class YggdrasilPlugin : ICommandPlugin 
 { 
    public string Name => "Auth.YggdrasilCommand"; 
  
    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig) 
     { 
         string authUrl = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing in step config"); 
         bool ignoreSslErrors = stepConfig.TryGetProperty("ignoreSslErrors", out var sslProp) && sslProp.GetBoolean(); 
  
         authUrl = Interpolate(authUrl, context); 
  
         string? username = context.Get<string>("username");
        string? password = context.Get<string>("password");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return new CommandResult(false, "Р›РѕРіРёРЅ РёР»Рё РїР°СЂРѕР»СЊ РЅРµ РїРµСЂРµРґР°РЅС‹ РІ РєРѕРЅРІРµР№РµСЂ.");
        }

        context.Reporter.ReportState("РђРІС‚РѕСЂРёР·Р°С†РёСЏ РЅР° СЃРµСЂРІРµСЂРµ..."); 
  
         var requestBody = new Dictionary<string, object>();

        foreach (var property in stepConfig.EnumerateObject())
        {
            if (property.Name == "url" || property.Name == "ignoreSslErrors") continue;

            string stringValue = property.Value.ValueKind == JsonValueKind.String
                ? Interpolate(property.Value.GetString() ?? "", context)
                : property.Value.GetRawText();

            requestBody[property.Name] = stringValue;
        } 
  
requestBody["username"] = username;
        requestBody["password"] = password;
        requestBody["clientToken"] = Guid.NewGuid().ToString("N"); 
  
         var handler = new HttpClientHandler();
        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
        }
        using var httpClient = new HttpClient(handler); 
  
var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"); 
  
         try
        {
            using var response = await httpClient.PostAsync(authUrl, content, context.CancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(context.CancellationToken); 
  
             if (string.IsNullOrWhiteSpace(responseText))
            {
                return new CommandResult(false, "РЎРµСЂРІРµСЂ Р°РІС‚РѕСЂРёР·Р°С†РёРё РІРµСЂРЅСѓР» РїСѓСЃС‚РѕР№ РѕС‚РІРµС‚.");
            } 
  
             using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement; 
  
             if (root.TryGetProperty("error", out var errorElement))
            {
                string errorMsg = root.TryGetProperty("errorMessage", out var errMsgElement)
                    ? errMsgElement.GetString() ?? "РќРµРёР·РІРµСЃС‚РЅР°СЏ РѕС€РёР±РєР° СЃРµСЂРІРµСЂР°"
                    : "РќРµРёР·РІРµСЃС‚РЅР°СЏ РѕС€РёР±РєР° СЃРµСЂРІРµСЂР°";

                return new CommandResult(false, $"РћС€РёР±РєР° Р°РІС‚РѕСЂРёР·Р°С†РёРё: {errorMsg}");
            } 
  
             if (root.TryGetProperty("has_access", out var accessElement) && !accessElement.GetBoolean())
            {
                return new CommandResult(false, "Р”РѕСЃС‚СѓРї Р·Р°РєСЂС‹С‚. РћРїР»Р°С‚РёС‚Рµ Р°РєС‚РёРІР°С†РёСЋ РЅР° СЃР°Р№С‚Рµ.");
            } 
  
             var profile = root.GetProperty("selectedProfile");

            context.Set("accessToken", root.GetProperty("accessToken").GetString() ?? "");
            context.Set("clientToken", root.GetProperty("clientToken").GetString() ?? "");
            context.Set("uuid", profile.GetProperty("id").GetString() ?? "");
            context.Set("playerName", profile.GetProperty("name").GetString() ?? "Player");

            if (root.TryGetProperty("is_admin", out var isAdmin)) context.Set("is_admin", isAdmin.GetBoolean());
            if (root.TryGetProperty("is_tester", out var isTester)) context.Set("is_tester", isTester.GetBoolean()); 
  
context.Reporter.ReportState("РђРІС‚РѕСЂРёР·Р°С†РёСЏ СѓСЃРїРµС€РЅР°."); 
         }
        catch (Exception ex)
        {
            return new CommandResult(false, $"РЎР±РѕР№ РїСЂРё РѕР±СЂР°С‰РµРЅРёРё Рє СЃРµСЂРІРµСЂСѓ Р°РІС‚РѕСЂРёР·Р°С†РёРё: {ex.Message}");
        }

        return new CommandResult(true);
    } 
  
     private string Interpolate(string text, CommandContext context)
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
  
public ValueTask DisposeAsync() => ValueTask.CompletedTask;
} 
