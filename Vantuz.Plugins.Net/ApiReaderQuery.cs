namespace Vantuz.Plugins.Net;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Query: Чтение данных из API.
/// Per Armatura:76-78 - только чтение, нет side effects.
/// </summary>
public class ApiReaderQuery : IQueryPlugin
{
    public string Name => "Net.ApiReader";

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        string url = stepConfig.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("URL is missing in step config");

        string payloadKey = stepConfig.GetProperty("payloadKey").GetString()
            ?? throw new InvalidOperationException("payloadKey is missing in step config");

        bool ignoreSslErrors = stepConfig.TryGetProperty("ignoreSslErrors", out var sslProp)
            && sslProp.GetBoolean();

        string? fallback = stepConfig.TryGetProperty("fallback", out var fallbackProp)
            ? fallbackProp.GetString()
            : null;

        url = Interpolate(url, context);

        // Анти-кэш
        url = url.Contains('?')
            ? $"{url}&t={DateTime.UtcNow.Ticks}"
            : $"{url}?t={DateTime.UtcNow.Ticks}";

        context.Reporter.ReportState($"Reading API: {url}...");

        var handler = new HttpClientHandler();
        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, sslPolicyErrors) => true;
        }

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher/2.0");

        try
        {
            using var response = await httpClient.GetAsync(url, context.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(context.CancellationToken);
                result = Interpolate(result.Trim(), context);
                return new ApiReaderResult(payloadKey, result);
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
                var fallbackResult = Interpolate(fallback, context);
                return new ApiReaderResult(payloadKey, fallbackResult);
            }
            else
            {
                throw new InvalidOperationException($"Ошибка ApiReader при запросе {url}: {ex.Message}", ex);
            }
        }
    }

    private static string Interpolate(string text, QueryContext context)
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

/// <summary>
/// Результат выполнения ApiReaderQuery для передачи через мутации.
/// </summary>
public record ApiReaderResult(string PayloadKey, string Data); 
