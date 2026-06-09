namespace Vantuz.Core;

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ARM002/ARM003: Virtual Descriptor СЃРёСЃС‚РµРјР° РІРјРµСЃС‚Рѕ raw I/O Stream РѕР±СЉРµРєС‚РѕРІ.
/// РџСЂРµРґСЃС‚Р°РІР»СЏРµС‚ СЃРѕР±РѕР№ Р»РµРіРєРѕРІРµСЃРЅС‹Р№ integer-based С‚РѕРєРµРЅ РґР»СЏ РґРѕСЃС‚СѓРїР° Рє РґР°РЅРЅС‹Рј.
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
    /// РЎРѕР·РґР°РµС‚ VirtualDescriptor РёР· byte array (РІ РїР°РјСЏС‚Рё)
    /// </summary>
    /// F_doc: {FromMemory returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies FromMemory behavior
    public static VirtualDescriptor FromMemory(byte[] data, string? contentType = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return new VirtualDescriptor(DescriptorType.Memory, data.Length, contentType, data);
    }

    /// <summary>
    /// РЎРѕР·РґР°РµС‚ VirtualDescriptor РёР· СЃС‚СЂРѕРєРё (РІ РїР°РјСЏС‚Рё)
    /// </summary>
    /// F_doc: {FromString returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies FromString behavior
    public static VirtualDescriptor FromString(string content, string? contentType = "text/plain")
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new VirtualDescriptor(DescriptorType.Memory, bytes.Length, contentType, bytes);
    }

    /// <summary>
    /// РђСЃРёРЅС…СЂРѕРЅРЅРѕ СЃРѕР·РґР°РµС‚ VirtualDescriptor РёР· С„Р°Р№Р»Р° (Р·Р°РіСЂСѓР¶Р°РµС‚ РІ РїР°РјСЏС‚СЊ)
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
    /// РђСЃРёРЅС…СЂРѕРЅРЅРѕ СЃРѕР·РґР°РµС‚ VirtualDescriptor РёР· HTTP РѕС‚РІРµС‚Р°
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
    /// РџРѕР»СѓС‡Р°РµС‚ РґР°РЅРЅС‹Рµ РєР°Рє ReadOnlyMemory<byte>
    /// </summary>
    /// F_doc: {GetData returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GetData behavior
    public ReadOnlyMemory<byte> GetData()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");
        return _data.AsMemory();
    }

    /// <summary>
    /// РџРѕР»СѓС‡Р°РµС‚ РґР°РЅРЅС‹Рµ РєР°Рє СЃС‚СЂРѕРєСѓ
    /// </summary>
    /// F_doc: {GetString returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GetString behavior
    public string GetString()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) return string.Empty;
        return System.Text.Encoding.UTF8.GetString(_data);
    }

    /// <summary>
    /// Р—Р°РїРёСЃС‹РІР°РµС‚ РґР°РЅРЅС‹Рµ РІ РїРѕС‚РѕРє
    /// </summary>
    public async Task WriteToStreamAsync(Stream target, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");
        if (target == null) throw new ArgumentNullException(nameof(target));

        await target.WriteAsync(_data.AsMemory(0, _data.Length), ct);
    }

    /// <summary>
    /// РђСЃРёРЅС…СЂРѕРЅРЅРѕ СЃРѕС…СЂР°РЅСЏРµС‚ РґР°РЅРЅС‹Рµ РІ С„Р°Р№Р».
    /// Per INVARIANT_THEORY В§3.2 Nomadic Invariant: rejects absolute paths to enforce host portability.
    /// F_doc: {absolute path passed to SaveToFileAsync}
    /// E_doc: Unit test with Path.IsPathRooted validation
    /// </summary>
    public async Task SaveToFileAsync(string filePath, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualDescriptor));
        if (_data == null) throw new InvalidOperationException("No data available");
        if (Path.IsPathRooted(filePath))
            throw new ArgumentException("Absolute paths violate the Nomadic Invariant. Use relative paths or ${special:Folder} interpolation.", nameof(filePath));

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
/// F_doc: {DescriptorType returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DescriptorType behavior

public enum DescriptorType
{
    Memory,
    File,
    HttpResponse,
    NetworkSocket
}

/// <summary>
/// Extension methods РґР»СЏ РёРЅС‚РµРіСЂР°С†РёРё VirtualDescriptor СЃ CommandContext Рё QueryContext
/// </summary>
/// F_doc: {VirtualDescriptorExtensions returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies VirtualDescriptorExtensions behavior
public static class VirtualDescriptorExtensions
{
    /// F_doc: {SetDescriptor returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies SetDescriptor behavior
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
