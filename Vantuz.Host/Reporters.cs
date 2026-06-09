using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Host;

/// <summary>
/// РђСЃРёРЅС…СЂРѕРЅРЅС‹Р№ СЂРµРїРѕСЂС‚РµСЂ, Р·Р°РїРёСЃС‹РІР°СЋС‰РёР№ Р»РѕРіРё РІ С„Р°Р№Р».
/// Р’РќРРњРђРќРР•: РўРµРєСѓС‰Р°СЏ СЂРµР°Р»РёР·Р°С†РёСЏ РёСЃРїРѕР»СЊР·СѓРµС‚ Unbounded Channel, С‡С‚Рѕ РјРѕР¶РµС‚ РїСЂРёРІРµСЃС‚Рё Рє OOM.
/// </summary>
public class AsyncFileReporter : IStatusReporter, IAsyncDisposable
{
    private readonly Channel<string> _channel;
    private readonly StreamWriter _writer;
    private readonly Task _processTask;

    public AsyncFileReporter(string filePath)
    {
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        
        // РћРіСЂР°РЅРёС‡РёРІР°РµРј РѕС‡РµСЂРµРґСЊ РґРѕ 10 000 СЃРѕРѕР±С‰РµРЅРёР№ (РѕРєРѕР»Рѕ РїР°СЂС‹ РјРµРіР°Р±Р°Р№С‚ РћР—РЈ). 
        // Р•СЃР»Рё РґРёСЃРє Р·Р°РІРёСЃ Рё РѕС‡РµСЂРµРґСЊ Р·Р°РїРѕР»РЅРёР»Р°СЃСЊ, РЅРѕРІС‹Рµ Р»РѕРіРё Р±СѓРґСѓС‚ РїСЂРѕСЃС‚Рѕ РѕС‚Р±СЂР°СЃС‹РІР°С‚СЊСЃСЏ (DropWrite), 
        // РіР°СЂР°РЅС‚РёСЂСѓСЏ, С‡С‚Рѕ Р»Р°СѓРЅС‡РµСЂ РќРРљРћР“Р”Рђ РЅРµ СѓРїР°РґРµС‚ РёР·-Р·Р° РЅРµС…РІР°С‚РєРё РїР°РјСЏС‚Рё. 
        var options = new System.Threading.Channels.BoundedChannelOptions(10000) 
        { 
            SingleReader = true, 
            SingleWriter = false, 
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite 
        }; 
        _channel = System.Threading.Channels.Channel.CreateBounded<string>(options);
        _processTask = ProcessLogsAsync();
    }
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

    public void ReportState(string message)
    {
        _channel.Writer.TryWrite($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [STATE] {message}");
    }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

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
                // РџРµСЂРёРѕРґРёС‡РµСЃРєРёР№ СЃР±СЂРѕСЃ Р±СѓС„РµСЂР° РґР»СЏ РЅР°РґРµР¶РЅРѕСЃС‚Рё
                await _writer.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"РћС€РёР±РєР° РІ AsyncFileReporter: {ex.Message}");
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
