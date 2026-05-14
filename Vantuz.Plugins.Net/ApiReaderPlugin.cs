namespace Vantuz.Plugins.Net; 
  
 using System; 
 using System.Net.Http; 
 using System.Text.Json; 
 using System.Threading.Tasks; 
 using Vantuz.Core; 
  
 public class ApiReaderPlugin : IVantuzPlugin 
 { 
     public string Name => "Net.ApiReader"; 
  
     public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
     { 
         string url = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing in step config"); 
         string payloadKey = stepConfig.GetProperty("payloadKey").GetString() ?? throw new Exception("payloadKey is missing in step config"); 
         bool ignoreSslErrors = stepConfig.TryGetProperty("ignoreSslErrors", out var sslProp) && sslProp.GetBoolean(); 
         string? fallback = stepConfig.TryGetProperty("fallback", out var fallbackProp) ? fallbackProp.GetString() : null; 
  
         url = Interpolate(url, context); 
          
         // Анти-кэш 
         url = url.Contains('?') ? $"{url}&t={DateTime.UtcNow.Ticks}" : $"{url}?t={DateTime.UtcNow.Ticks}"; 
  
         context.Reporter.ReportState($"Reading API: {url}..."); 
  
         var handler = new HttpClientHandler(); 
         if (ignoreSslErrors) 
         { 
             handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true; 
         } 
  
         using var httpClient = new HttpClient(handler) 
         { 
             Timeout = TimeSpan.FromSeconds(5) // Защита от зависаний 
         }; 
          
         // Маскировка от ботов 
         httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher/2.0"); 
  
         try 
         { 
             using var response = await httpClient.GetAsync(url, context.CancellationToken); 
              
             if (response.IsSuccessStatusCode) 
             { 
                 var result = await response.Content.ReadAsStringAsync(context.CancellationToken); 
                 result = Interpolate(result.Trim(), context); 
                 context.Set(payloadKey, result); 
             } 
             else 
             { 
                 throw new HttpRequestException($"HTTP Error: {response.StatusCode}"); 
             } 
         } 
         catch (Exception ex) 
         { 
             // Паттерн Fallback (Graceful Degradation) 
             if (fallback != null) 
             { 
                 context.Reporter.ReportState($"Сетевая ошибка API. Используем fallback для {payloadKey}."); 
                 context.Set(payloadKey, Interpolate(fallback, context)); 
             } 
             else 
             { 
                 context.Abort($"Ошибка ApiReader при запросе {url}: {ex.Message}"); 
                 return; 
             } 
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
