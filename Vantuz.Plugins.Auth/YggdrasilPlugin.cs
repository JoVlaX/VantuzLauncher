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
            return new CommandResult(false, "Логин или пароль не переданы в конвейер.");
        }

        context.Reporter.ReportState("Авторизация на сервере..."); 
  
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
                return new CommandResult(false, "Сервер авторизации вернул пустой ответ.");
            } 
  
             using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement; 
  
             if (root.TryGetProperty("error", out var errorElement))
            {
                string errorMsg = root.TryGetProperty("errorMessage", out var errMsgElement)
                    ? errMsgElement.GetString() ?? "Неизвестная ошибка сервера"
                    : "Неизвестная ошибка сервера";

                return new CommandResult(false, $"Ошибка авторизации: {errorMsg}");
            } 
  
             if (root.TryGetProperty("has_access", out var accessElement) && !accessElement.GetBoolean())
            {
                return new CommandResult(false, "Доступ закрыт. Оплатите активацию на сайте.");
            } 
  
             var profile = root.GetProperty("selectedProfile");

            context.Set("accessToken", root.GetProperty("accessToken").GetString() ?? "");
            context.Set("clientToken", root.GetProperty("clientToken").GetString() ?? "");
            context.Set("uuid", profile.GetProperty("id").GetString() ?? "");
            context.Set("playerName", profile.GetProperty("name").GetString() ?? "Player");

            if (root.TryGetProperty("is_admin", out var isAdmin)) context.Set("is_admin", isAdmin.GetBoolean());
            if (root.TryGetProperty("is_tester", out var isTester)) context.Set("is_tester", isTester.GetBoolean()); 
  
context.Reporter.ReportState("Авторизация успешна."); 
         }
        catch (Exception ex)
        {
            return new CommandResult(false, $"Сбой при обращении к серверу авторизации: {ex.Message}");
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
  
public ValueTask DisposeAsync() => ValueTask.CompletedTask;
} 
