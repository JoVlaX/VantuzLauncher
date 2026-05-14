using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Host;

/// <summary>
/// Асинхронный репортер, записывающий логи в файл.
/// ВНИМАНИЕ: Текущая реализация использует Unbounded Channel, что может привести к OOM.
/// </summary>
public class AsyncFileReporter : IStatusReporter, IAsyncDisposable
{
    private readonly Channel<string> _channel;
    private readonly StreamWriter _writer;
    private readonly Task _processTask;

    public AsyncFileReporter(string filePath)
    {
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        
        // Ограничиваем очередь до 10 000 сообщений (около пары мегабайт ОЗУ). 
        // Если диск завис и очередь заполнилась, новые логи будут просто отбрасываться (DropWrite), 
        // гарантируя, что лаунчер НИКОГДА не упадет из-за нехватки памяти. 
        var options = new System.Threading.Channels.BoundedChannelOptions(10000) 
        { 
            SingleReader = true, 
            SingleWriter = false, 
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite 
        }; 
        _channel = System.Threading.Channels.Channel.CreateBounded<string>(options);
        _processTask = ProcessLogsAsync();
    }

    public void ReportState(string message)
    {
        _channel.Writer.TryWrite($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [STATE] {message}");
    }

    public void ReportProgress(string taskName, double percentage)
    {
        _channel.Writer.TryWrite($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PROGRESS] {taskName}: {percentage:F1}%");
    }

    private async Task ProcessLogsAsync()
    {
        try
        {
            await foreach (var log in _channel.Reader.ReadAllAsync())
            {
                await _writer.WriteLineAsync(log);
                // Периодический сброс буфера для надежности
                await _writer.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в AsyncFileReporter: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        try
        {
            await _processTask;
        }
        catch { }
        finally
        {
            await _writer.DisposeAsync();
        }
    }
}
