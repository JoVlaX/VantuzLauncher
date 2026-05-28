namespace Vantuz.Plugins.Net;

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Пример QuantizedNode для скачивания файлов.
/// Демонстрирует квантованное выполнение: читает данные чанками и уступает квант при необходимости.
/// </summary>
public sealed class QuantumDownloaderNode : QuantizedNode
{
    private readonly HttpClient _httpClient;
    private DownloadState? _state;

    public override string Name => "Net.QuantumDownloader";

    public QuantumDownloaderNode()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 VantuzLauncher/2.0");
    }

    public override async Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct)
    {
        // Инициализация при первом кванте
        if (_state == null)
        {
            var url = stepConfig.GetProperty("url").GetString()
                ?? throw new ArgumentException("url is required");
            var destination = stepConfig.GetProperty("destination").GetString()
                ?? throw new ArgumentException("destination is required");

            url = Interpolate(url, context);
            destination = Interpolate(destination, context);

            _state = new DownloadState
            {
                Url = url,
                Destination = destination,
                TotalBytes = 0,
                DownloadedBytes = 0,
                Buffer = new byte[8192],
                ResponseStream = null,
                FileStream = null
            };

            // Открываем соединение
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            _state.TotalBytes = response.Content.Headers.ContentLength ?? -1;
            _state.ResponseStream = await response.Content.ReadAsStreamAsync(ct);

            var dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            _state.FileStream = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                8192,
                true);

            context.Reporter.ReportState($"Downloading {Path.GetFileName(destination)}...");
        }

        // Читаем данные чанками пока есть время в кванте
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int chunkCount = 0;

        while (_state.DownloadedBytes < _state.TotalBytes || _state.TotalBytes == -1)
        {
            ct.ThrowIfCancellationRequested();

            // Проверяем остаток кванта (оставляем запас 2мс на обработку)
            if (context.RemainingQuantum < TimeSpan.FromMilliseconds(2))
            {
                // Уступаем квант, вернёмся в следующем
                return QuantumResult.Yield(_state);
            }

            // Читаем чанк
            int bytesRead = await _state.ResponseStream!.ReadAsync(
                _state.Buffer.AsMemory(0, _state.Buffer.Length), ct);

            if (bytesRead == 0)
            {
                // Файл полностью скачан
                await _state.FileStream!.DisposeAsync();
                await _state.ResponseStream!.DisposeAsync();

                context.Reporter.ReportState("Download completed.");

                // Сохраняем результат
                context.Mutations.Set("downloadedFile", _state.Destination);

                _state = null;
                return QuantumResult.Complete();
            }

            // Пишем в файл
            await _state.FileStream!.WriteAsync(_state.Buffer.AsMemory(0, bytesRead), ct);
            _state.DownloadedBytes += bytesRead;
            chunkCount++;

            // Обновляем прогресс каждые 10 чанков или если время кванта подходит к концу
            if (chunkCount % 10 == 0 || context.RemainingQuantum < TimeSpan.FromMilliseconds(5))
            {
                if (_state.TotalBytes > 0)
                {
                    double progress = (double)_state.DownloadedBytes / _state.TotalBytes * 100;
                    context.Reporter.ReportProgress(Name, progress);
                }
            }
        }

        // Должны были обработать выше, но на всякий случай
        return QuantumResult.Complete();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_state?.FileStream != null)
            await _state.FileStream.DisposeAsync();
        if (_state?.ResponseStream != null)
            await _state.ResponseStream.DisposeAsync();
        _httpClient.Dispose();
    }

    private string Interpolate(string text, IQuantumContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Получаем все ключи из payload
        // Note: В реальном коде здесь был бы доступ ко всем payload
        return text; // Упрощённо
    }

    /// <summary>
    /// Состояние скачивания, сохраняемое между квантами
    /// </summary>
    private class DownloadState
    {
        public string Url { get; set; } = "";
        public string Destination { get; set; } = "";
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public byte[] Buffer { get; set; } = Array.Empty<byte>();
        public Stream? ResponseStream { get; set; }
        public Stream? FileStream { get; set; }
    }
}
