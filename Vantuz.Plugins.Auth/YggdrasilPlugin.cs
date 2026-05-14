namespace Vantuz.Plugins.Auth; 
  
 using System; 
 using System.Collections.Generic; 
 using System.Net.Http; 
 using System.Text; 
 using System.Text.Json; 
 using System.Threading.Tasks; 
 using Vantuz.Core; 
  
 public class YggdrasilPlugin : IVantuzPlugin 
 { 
     public string Name => "Auth.Yggdrasil"; 
  
     public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
     { 
         string authUrl = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing in step config"); 
         bool ignoreSslErrors = stepConfig.TryGetProperty("ignoreSslErrors", out var sslProp) && sslProp.GetBoolean(); 
  
         authUrl = Interpolate(authUrl, context); 
  
         string? username = context.Get<string>("username"); 
         string? password = context.Get<string>("password"); 
  
         if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) 
         { 
             context.Abort("Логин или пароль не переданы в конвейер."); 
             return; 
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
                 context.Abort("Сервер авторизации вернул пустой ответ."); 
                 return; 
             } 
  
             using var doc = JsonDocument.Parse(responseText); 
             var root = doc.RootElement; 
  
             if (root.TryGetProperty("error", out var errorElement)) 
             { 
                 string errorMsg = root.TryGetProperty("errorMessage", out var errMsgElement)  
                     ? errMsgElement.GetString() ?? "Неизвестная ошибка сервера"  
                     : "Неизвестная ошибка сервера"; 
                  
                 context.Abort($"Ошибка авторизации: {errorMsg}"); 
                 return; 
             } 
  
             if (root.TryGetProperty("has_access", out var accessElement) && !accessElement.GetBoolean()) 
             { 
                 context.Abort("Доступ закрыт. Оплатите активацию на сайте."); 
                 return; 
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
             context.Abort($"Сбой при обращении к серверу авторизации: {ex.Message}"); 
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
