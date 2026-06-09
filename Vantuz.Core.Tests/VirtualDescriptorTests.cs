namespace Vantuz.Core.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using Vantuz.Core;
using Xunit;

/// <summary>
/// Tests for VirtualDescriptor вЂ” ARM002/ARM003 lightweight descriptor system.
/// Per INVARIANT_THEORY В§1.2: falsifiable claims about descriptor lifecycle.
/// </summary>
/// F_doc: {VirtualDescriptorTests returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies VirtualDescriptorTests behavior
public class VirtualDescriptorTests
{
    /// <summary>
    /// E_doc: FromMemory creates a valid descriptor with byte array data.
    /// F_doc: Null data throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void FromMemory_ValidData_CreatesDescriptor()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0x03 };
        var descriptor = VirtualDescriptor.FromMemory(data, "application/octet-stream");

        Assert.True(descriptor.IsValid);
        Assert.Equal(3, descriptor.Size);
        Assert.Equal("application/octet-stream", descriptor.ContentType);
        Assert.Equal(DescriptorType.Memory, descriptor.Type);
    }

    /// <summary>
    /// E_doc: FromMemory with null throws ArgumentNullException.
    /// F_doc: FromMemory(null) returns a descriptor instead of throwing.
    /// </summary>
    [Fact]
    public void FromMemory_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VirtualDescriptor.FromMemory(null!));
    }

    /// <summary>
    /// E_doc: FromString creates a descriptor from UTF-8 string.
    /// F_doc: GetString returns corrupted or empty data.
    /// </summary>
    [Fact]
    public void FromString_RoundTrip_ReturnsOriginalString()
    {
        const string original = "Hello, Armatura!";
        var descriptor = VirtualDescriptor.FromString(original);

        Assert.True(descriptor.IsValid);
        Assert.Equal(original, descriptor.GetString());
    }

    /// <summary>
    /// E_doc: WriteToStreamAsync copies data to target stream.
    /// F_doc: Stream contains wrong or incomplete data.
    /// </summary>
    [Fact]
    public async Task WriteToStreamAsync_CopiesDataCorrectly()
    {
        byte[] data = new byte[] { 0x0A, 0x0B, 0x0C };
        var descriptor = VirtualDescriptor.FromMemory(data);

        using var stream = new MemoryStream();
        await descriptor.WriteToStreamAsync(stream);

        Assert.Equal(data.Length, stream.Length);
        Assert.Equal(data, stream.ToArray());
    }

    /// <summary>
    /// E_doc: SaveToFileAsync writes data to a relative path file.
    /// F_doc: File missing or content mismatch.
    /// </summary>
    [Fact]
    public async Task SaveToFileAsync_RelativePath_WritesFile()
    {
        byte[] data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var descriptor = VirtualDescriptor.FromMemory(data);

        string tempFile = Path.Combine(Path.GetTempPath(), $"vd_test_{Guid.NewGuid()}.bin");
        // Use relative path from temp directory to satisfy nomadic invariant
        string relativePath = $"vd_test_{Guid.NewGuid()}.bin";
        string originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            await descriptor.SaveToFileAsync(relativePath);

            string fullPath = Path.Combine(Path.GetTempPath(), relativePath);
            Assert.True(File.Exists(fullPath));
            Assert.Equal(data, await File.ReadAllBytesAsync(fullPath));
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { File.Delete(Path.Combine(Path.GetTempPath(), relativePath)); } catch { }
        }
    }

    /// <summary>
    /// E_doc: SaveToFileAsync rejects absolute paths per Nomadic Invariant В§3.2.
    /// F_doc: Absolute path accepted without exception.
    /// </summary>
    [Fact]
    public async Task SaveToFileAsync_AbsolutePath_ThrowsArgumentException()
    {
        var descriptor = VirtualDescriptor.FromMemory(new byte[] { 0x01 });
        string absolutePath = Path.Combine(Path.GetTempPath(), "test.bin");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => descriptor.SaveToFileAsync(absolutePath));
        Assert.Contains("Nomadic Invariant", ex.Message);
    }

    /// <summary>
    /// E_doc: Dispose invalidates the descriptor.
    /// F_doc: Operations after Dispose succeed instead of throwing.
    /// </summary>
    [Fact]
    public void Dispose_MarksInvalid()
    {
        var descriptor = VirtualDescriptor.FromMemory(new byte[] { 0x01 });
        Assert.True(descriptor.IsValid);

        descriptor.Dispose();

        Assert.False(descriptor.IsValid);
        Assert.Throws<ObjectDisposedException>(() => descriptor.GetString());
    }

    /// <summary>
    /// E_doc: Id is unique across multiple descriptors.
    /// F_doc: Two descriptors share the same Id.
    /// </summary>
    [Fact]
    public void Id_UniqueAcrossInstances()
    {
        var d1 = VirtualDescriptor.FromMemory(new byte[] { 0x01 });
        var d2 = VirtualDescriptor.FromMemory(new byte[] { 0x02 });

        Assert.NotEqual(d1.Id, d2.Id);
    }
}
