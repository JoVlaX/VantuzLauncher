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
  
         url = Interpolate(url, context); 
         context.Reporter.ReportState($"Reading API: {url}..."); 
  
         var handler = new HttpClientHandler(); 
         if (ignoreSslErrors) 
         { 
             handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true; 
         } 
  
         using var httpClient = new HttpClient(handler); 
  
         try 
         { 
             using var response = await httpClient.GetAsync(url, context.CancellationToken); 
             response.EnsureSuccessStatusCode(); 
              
             var result = await response.Content.ReadAsStringAsync(context.CancellationToken); 
             result = Interpolate(result.Trim(), context); 
  
             context.Set(payloadKey, result); 
         } 
         catch (Exception ex) 
         { 
             context.Abort($"Ошибка ApiReader при запросе {url}: {ex.Message}"); 
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
