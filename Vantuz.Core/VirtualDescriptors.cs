namespace Vantuz.Core;

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ARM002/ARM003: Virtual Descriptor система вместо raw I/O Stream объектов.
/// Представляет собой легковесный integer-based токен для доступа к данным.
/// </summary>
public sealed class VirtualDescriptor : IDisposable
{
    private readonly int _id;
    private readonly DescriptorType _type;
    private readonly long _size;
    private readonly string? _contentType;
    private byte[]? _data;
    private bool _disposed;

    public int Id => _id;
    public DescriptorType Type => _type;
    public long Size => _size;
    public string? ContentType => _contentType;
    public bool IsValid => !_disposed && _data != null;

    private static int _nextId = 1;

    private VirtualDescriptor(DescriptorType type, long size, string? contentType, byte[]? data)
    {
        _id = Interlocked.Increment(ref _nextId);
        _type = type;
        _size = size;
        _contentType = contentType;
        _data = data;
    }

    /// <summary>
    /// Создает VirtualDescriptor из byte array (в памяти)
    /// </summary>
    public static VirtualDescriptor FromMemory(byte[] data, string? contentType = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return new VirtualDescriptor(DescriptorType.Memory, data.Length, contentType, data);
    }

    /// <summary>
    /// Создает VirtualDescriptor из строки (в памяти)
    /// </summary>
    public static VirtualDescriptor FromString(string content, string? contentType = "text/plain")
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new VirtualDescriptor(DescriptorType.Memory, bytes.Length, contentType, bytes);
    }

    /// <summary>
    /// Асинхронно создает VirtualDescriptor из файла (загружает в память)
    /// </summary>
    public static async Task<VirtualDescriptor> FromFileAsync(string filePath, string? contentType = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        byte[] data = await File.ReadAllBytesAsync(filePath, ct);
        
        return new VirtualDescriptor(DescriptorType.File, fileInfo.Length, contentType, data);
    }

    /// <summary>
    /// Асинхронно создает VirtualDescriptor из HTTP ответа
    /// </summary>
    public static async Task<VirtualDescriptor> FromHttpResponseAsync(Stream responseStream, long? contentLength, string? contentType = null, CancellationToken ct = default)
    {
        if (responseStream == null) throw new ArgumentNullException(nameof(responseStream));

        using var ms = new MemoryStream();
        await responseStream.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        return new VirtualDescriptor(DescriptorType.HttpResponse, data.Length, contentType, data);
    }

    /// <summary>
    /// Получает данные как ReadOnlyMemory<byte>
    /// </summary>
    public ReadOnlyMemory<byte> GetData()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");
        return _data.AsMemory();
    }

    /// <summary>
    /// Получает данные как строку
    /// </summary>
    public string GetString()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) return string.Empty;
        return System.Text.Encoding.UTF8.GetString(_data);
    }

    /// <summary>
    /// Записывает данные в поток
    /// </summary>
    public async Task WriteToStreamAsync(Stream target, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");
        if (target == null) throw new ArgumentNullException(nameof(target));

        await target.WriteAsync(_data.AsMemory(0, _data.Length), ct);
    }

    /// <summary>
    /// Асинхронно сохраняет данные в файл
    /// </summary>
    public async Task SaveToFileAsync(string filePath, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(filePath, _data, ct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _data = null;
            _disposed = true;
        }
    }
}

public enum DescriptorType
{
    Memory,
    File,
    HttpResponse,
    NetworkSocket
}

/// <summary>
/// Extension methods для интеграции VirtualDescriptor с ExecutionContext
/// </summary>
public static class VirtualDescriptorExtensions
{
    public static void SetDescriptor(this CommandContext context, string key, VirtualDescriptor descriptor)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        context.Set(key, descriptor);
    }

    public static VirtualDescriptor? GetDescriptor(this QueryContext context, string key)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        return context.Get<VirtualDescriptor>(key);
    }

    public static VirtualDescriptor? GetDescriptor(this CommandContext context, string key)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        return context.Get<VirtualDescriptor>(key);
    }
}
