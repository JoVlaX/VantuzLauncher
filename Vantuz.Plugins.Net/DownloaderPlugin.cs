namespace Vantuz.Plugins.Net;

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

public class DownloaderPlugin : IVantuzPlugin
{
    public string Name => "Net.Downloader";
    private readonly HttpClient _httpClient = new ();

    public async Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next)
    {
        string url = stepConfig.GetProperty("url").GetString() ?? throw new Exception("URL is missing");
        string destination = stepConfig.GetProperty("destination").GetString() ?? throw new Exception("Destination is missing");

        // Применяем интерполяцию Payload 
        url = Interpolate(url, context); 
        destination = Interpolate(destination, context); 

        context.Reporter.ReportState($"Downloading {Path.GetFileName(destination)}...");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(context.CancellationToken);
        
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, context.CancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, context.CancellationToken);
            totalRead += bytesRead;

            if (totalBytes != -1)
            {
                double progress = (double)totalRead / totalBytes * 100;
                context.Reporter.ReportProgress(Name, progress);
            }
        }

        context.Reporter.ReportState("Download completed.");
        await next(context);
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
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
}
