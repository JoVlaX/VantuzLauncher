namespace Vantuz.Plugins.Auth; 
 
using System; 
using System.Net.Http; 
using System.Text; 
using System.Text.Json; 
using System.Threading.Tasks; 
using Vantuz.Core; 
 
public class YggdrasilPlugin : IVantuzPlugin 
{ 
    public string Name => "Auth.Yggdrasil"; 
     
    // Переиспользуем HttpClient для сокетов, но будем очищать его в DisposeAsync 
    private readonly HttpClient _httpClient = new(); 
 
    public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next) 
    { 
        // 1. Payload-Driven: URL берется из манифеста, а не хардкодится 
        string authUrl = stepConfig.GetProperty("url").GetString() 
            ?? throw new Exception("URL is missing in step config"); 
 
        // 2. Извлекаем данные, которые UI прокинул в конвейер при старте 
        string? username = context.Get<string>("username"); 
        string? password = context.Get<string>("password"); 
 
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) 
        { 
            context.Abort("Логин или пароль не переданы в конвейер."); 
            return; 
        } 
 
        context.Reporter.ReportState("Авторизация на сервере..."); 
 
        var requestBody = new 
        { 
            username = username, 
            password = password, 
            clientToken = Guid.NewGuid().ToString("N") 
        }; 
 
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"); 
 
        try 
        { 
            using var response = await _httpClient.PostAsync(authUrl, content, context.CancellationToken); 
            var responseText = await response.Content.ReadAsStringAsync(context.CancellationToken); 
 
            using var doc = JsonDocument.Parse(responseText); 
            var root = doc.RootElement; 
 
            // 3. Short-circuiting: Проверяем ошибки API Yggdrasil 
            if (root.TryGetProperty("error", out var errorElement)) 
            { 
                string errorMsg = root.TryGetProperty("errorMessage", out var errMsgElement) 
                    ? errMsgElement.GetString() ?? "Неизвестная ошибка сервера" 
                    : "Неизвестная ошибка сервера"; 
                 
                context.Abort($"Ошибка авторизации: {errorMsg}"); 
                return; 
            } 
 
            // Проверка кастомного флага доступа (из старого кода) 
            if (root.TryGetProperty("has_access", out var accessElement) && !accessElement.GetBoolean()) 
            { 
                context.Abort("Доступ закрыт. Оплатите активацию на сайте."); 
                return; 
            } 
 
            // 4. Обогащение Payload: складываем результаты для следующих плагинов 
            var profile = root.GetProperty("selectedProfile"); 
             
            context.Set("accessToken", root.GetProperty("accessToken").GetString()!); 
            context.Set("clientToken", root.GetProperty("clientToken").GetString()!); 
            context.Set("uuid", profile.GetProperty("id").GetString()!); 
            context.Set("playerName", profile.GetProperty("name").GetString()!); 
             
            // Флаги ролей (например, для выбора ветки модов Packwiz в следующих шагах) 
            if (root.TryGetProperty("is_admin", out var isAdmin)) context.Set("is_admin", isAdmin.GetBoolean()); 
            if (root.TryGetProperty("is_tester", out var isTester)) context.Set("is_tester", isTester.GetBoolean()); 
 
            context.Reporter.ReportState("Авторизация успешна."); 
        } 
        catch (Exception ex) when (ex is not OperationCanceledException) 
        { 
            context.Abort($"Ошибка соединения с сервером авторизации: {ex.Message}"); 
            return; 
        } 
 
        // 5. Передаем выполнение следующему плагину в цепи 
        await next(context); 
    } 
 
    public ValueTask DisposeAsync() 
    { 
        _httpClient.Dispose(); 
        return ValueTask.CompletedTask; 
    } 
}