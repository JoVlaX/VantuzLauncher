namespace Vantuz.Plugins.Net;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Query: Р§С‚РµРЅРёРµ РґР°РЅРЅС‹С… РёР· API.
/// Per Armatura:76-78 - С‚РѕР»СЊРєРѕ С‡С‚РµРЅРёРµ, РЅРµС‚ side effects.
/// F_doc: {HTTP request returns non-2xx or payloadKey missing from response}
/// E_doc: Unit test with HttpMessageHandler mock returning 404
/// </summary>
public class ApiReaderQuery : IQueryPlugin
{
    public string Name => "Net.ApiReaderQuery";

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

        // РђРЅС‚Рё-РєСЌС€
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
            // РџР°С‚С‚РµСЂРЅ Fallback (Graceful Degradation)
            if (fallback != null)
            {
                context.Reporter.ReportState($"РЎРµС‚РµРІР°СЏ РѕС€РёР±РєР° API. РСЃРїРѕР»СЊР·СѓРµРј fallback РґР»СЏ {payloadKey}.");
                var fallbackResult = Interpolate(fallback, context);
                return new ApiReaderResult(payloadKey, fallbackResult);
            }
            else
            {
                throw new InvalidOperationException($"РћС€РёР±РєР° ApiReader РїСЂРё Р·Р°РїСЂРѕСЃРµ {url}: {ex.Message}", ex);
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
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Р РµР·СѓР»СЊС‚Р°С‚ РІС‹РїРѕР»РЅРµРЅРёСЏ ApiReaderQuery РґР»СЏ РїРµСЂРµРґР°С‡Рё С‡РµСЂРµР· РјСѓС‚Р°С†РёРё.
/// </summary>
/// F_doc: {ApiReaderResult returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ApiReaderResult behavior
public record ApiReaderResult(string PayloadKey, string Data); 
