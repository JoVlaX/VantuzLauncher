namespace Vantuz.Core.Tests;

using System.IO;
using Vantuz.Plugins.Minecraft;
using Xunit;

/// <summary>
/// Tests for MinecraftGameQueryProvider вЂ” the layer that CmlLib uses to check version
/// existence and parse Forge version strings.
/// Per INVARIANT_THEORY.md В§1.2: falsifiable claims about version detection.
/// </summary>
/// F_doc: {MinecraftGameQueryProviderTests returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies MinecraftGameQueryProviderTests behavior
public class MinecraftGameQueryProviderTests
{
    /// <summary>
    /// E_doc: "1.20.1-forge-47.3.0" parses into mcVersion="1.20.1", forgeVersion="47.3.0".
    /// F_doc: This is the exact version string used in boot.json manifests.
    /// </summary>
    [Fact]
    public void ParseForgeVersion_StandardFormat_ReturnsCorrectTuple()
    {
        var (mcVersion, forgeVersion) = MinecraftGameQueryProvider.ParseForgeVersion("1.20.1-forge-47.3.0");

        Assert.Equal("1.20.1", mcVersion);
        Assert.Equal("47.3.0", forgeVersion);
    }

    /// <summary>
    /// E_doc: Fallback split handles edge cases with multiple dashes.
    /// F_doc: "1.20.1-forge-47.3.0" also works via fallback.
    /// </summary>
    [Fact]
    public void ParseForgeVersion_FallbackSplit_ReturnsCorrectTuple()
    {
        // This format triggers the fallback path because it has 3+ parts
        var (mcVersion, forgeVersion) = MinecraftGameQueryProvider.ParseForgeVersion("1.19.2-forge-43.2.0");

        Assert.Equal("1.19.2", mcVersion);
        Assert.Equal("43.2.0", forgeVersion);
    }

    private static string MakeForgeJson(string id = "1.20.1-forge-47.3.0", string inheritsFrom = "1.20.1") =>
        $"{{\"id\":\"{id}\",\"inheritsFrom\":\"{inheritsFrom}\",\"libraries\":[{{\"name\":\"cpw.mods:bootstraplauncher:1.1.2\",\"downloads\":{{\"artifact\":{{\"path\":\"cpw/mods/bootstraplauncher/1.1.2/bootstraplauncher-1.1.2.jar\"}}}}}},{{\"name\":\"cpw.mods:securejarhandler:2.1.10\",\"downloads\":{{\"artifact\":{{\"path\":\"cpw/mods/securejarhandler/2.1.10/securejarhandler-2.1.10.jar\"}}}}}},{{\"name\":\"net.minecraftforge:fmlloader:1.20.1-47.3.0\",\"downloads\":{{\"artifact\":{{\"path\":\"net/minecraftforge/fmlloader/1.20.1-47.3.0/fmlloader-1.20.1-47.3.0.jar\"}}}}}}]}}";

    /// <summary>
    /// E_doc: When Forge version JSON, all critical libraries, and vanilla client JAR exist,
    /// CheckVersionAsync returns Exists=true.
    /// F_doc: Forge does not create a version JAR; libraries from JSON + vanilla JAR are the proxy.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_ForgeComplete_ReturnsTrue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        string versionsDir = Path.Combine(mcDir, "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");

        // Create all critical libraries
        string bootstrapDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "bootstraplauncher", "1.1.2");
        Directory.CreateDirectory(bootstrapDir);
        await File.WriteAllBytesAsync(Path.Combine(bootstrapDir, "bootstraplauncher-1.1.2.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string secureDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "securejarhandler", "2.1.10");
        Directory.CreateDirectory(secureDir);
        await File.WriteAllBytesAsync(Path.Combine(secureDir, "securejarhandler-2.1.10.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string fmlLoaderDir = Path.Combine(mcDir, "libraries", "net", "minecraftforge", "fmlloader", "1.20.1-47.3.0");
        Directory.CreateDirectory(fmlLoaderDir);
        await File.WriteAllBytesAsync(Path.Combine(fmlLoaderDir, "fmlloader-1.20.1-47.3.0.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        // Vanilla client JAR (inheritsFrom)
        string vanillaDir = Path.Combine(mcDir, "versions", "1.20.1");
        Directory.CreateDirectory(vanillaDir);
        await File.WriteAllBytesAsync(Path.Combine(vanillaDir, "1.20.1.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        await File.WriteAllTextAsync(versionJsonPath, MakeForgeJson());

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", mcDir, default);

            Assert.True(result.Exists, $"Expected Exists=true but got: {result.ErrorMessage}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When vanilla version JSON and JAR both exist,
    /// CheckVersionAsync returns Exists=true.
    /// F_doc: Vanilla Minecraft creates both JSON descriptor and client JAR.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_VanillaComplete_ReturnsTrue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string versionsDir = Path.Combine(tempDir, ".minecraft", "versions", "1.20.1");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1.json");
        string versionJarPath = Path.Combine(versionsDir, "1.20.1.jar");
        await File.WriteAllTextAsync(versionJsonPath, "{\"id\":\"1.20.1\"}");
        await File.WriteAllBytesAsync(versionJarPath, new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1", Path.Combine(tempDir, ".minecraft"), default);

            Assert.True(result.Exists, $"Expected Exists=true but got: {result.ErrorMessage}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When Forge JSON exists but the critical fmlloader library is missing,
    /// CheckVersionAsync returns Exists=false.
    /// F_doc: This reproduces the 2026-06-07 crash where interrupted Forge install left JSON
    /// but not libraries, causing ClassNotFoundException at launch.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_ForgeIncomplete_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        string versionsDir = Path.Combine(mcDir, "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");

        // Create bootstraplauncher and securejarhandler, but NOT fmlloader
        string bootstrapDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "bootstraplauncher", "1.1.2");
        Directory.CreateDirectory(bootstrapDir);
        await File.WriteAllBytesAsync(Path.Combine(bootstrapDir, "bootstraplauncher-1.1.2.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string secureDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "securejarhandler", "2.1.10");
        Directory.CreateDirectory(secureDir);
        await File.WriteAllBytesAsync(Path.Combine(secureDir, "securejarhandler-2.1.10.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        // Vanilla client JAR (inheritsFrom)
        string vanillaDir = Path.Combine(mcDir, "versions", "1.20.1");
        Directory.CreateDirectory(vanillaDir);
        await File.WriteAllBytesAsync(Path.Combine(vanillaDir, "1.20.1.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        await File.WriteAllTextAsync(versionJsonPath, MakeForgeJson());

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", mcDir, default);

            Assert.False(result.Exists, "Expected Exists=false when fmlloader is missing");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When Forge JSON and fmlloader exist but bootstraplauncher is missing,
    /// CheckVersionAsync returns Exists=false.
    /// F_doc: This reproduces the 2026-06-07 crash where fmlloader was present but
    /// bootstraplauncher was absent, causing ClassNotFoundException at launch.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_ForgeBootstrapLauncherMissing_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        string versionsDir = Path.Combine(mcDir, "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");

        // Create fmlloader and securejarhandler, but NOT bootstraplauncher
        string secureDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "securejarhandler", "2.1.10");
        Directory.CreateDirectory(secureDir);
        await File.WriteAllBytesAsync(Path.Combine(secureDir, "securejarhandler-2.1.10.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string fmlLoaderDir = Path.Combine(mcDir, "libraries", "net", "minecraftforge", "fmlloader", "1.20.1-47.3.0");
        Directory.CreateDirectory(fmlLoaderDir);
        await File.WriteAllBytesAsync(Path.Combine(fmlLoaderDir, "fmlloader-1.20.1-47.3.0.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        // Vanilla client JAR (inheritsFrom)
        string vanillaDir = Path.Combine(mcDir, "versions", "1.20.1");
        Directory.CreateDirectory(vanillaDir);
        await File.WriteAllBytesAsync(Path.Combine(vanillaDir, "1.20.1.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        await File.WriteAllTextAsync(versionJsonPath, MakeForgeJson());

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", mcDir, default);

            Assert.False(result.Exists, "Expected Exists=false when bootstraplauncher is missing");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When vanilla JSON exists but JAR is missing, CheckVersionAsync returns Exists=false.
    /// F_doc: Interrupted vanilla download leaves JSON but not client JAR.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_VanillaIncomplete_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string versionsDir = Path.Combine(tempDir, ".minecraft", "versions", "1.20.1");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1.json");
        await File.WriteAllTextAsync(versionJsonPath, "{\"id\":\"1.20.1\"}");
        // Intentionally NOT creating the JAR

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1", Path.Combine(tempDir, ".minecraft"), default);

            Assert.False(result.Exists, "Expected Exists=false when JSON exists but JAR is missing");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When the version JSON is missing, CheckVersionAsync returns Exists=false.
    /// F_doc: This is the signal that triggers InstallVersionAsync in the pipeline.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_MissingVersion_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        Directory.CreateDirectory(mcDir);

        try
        {
            var provider = new MinecraftGameQueryProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", mcDir, default);

            Assert.False(result.Exists);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When Forge version JSON and all critical libraries already exist,
    /// GameInstallerCommand skips install and returns success.
    /// F_doc: This prevents unnecessary re-downloads and avoids the Forge installer timeout path entirely.
    /// </summary>
    [Fact]
    public async Task GameInstallerCommand_ForgeAlreadyInstalled_SkipsInstall()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        string versionsDir = Path.Combine(mcDir, "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");

        // All critical libraries + vanilla JAR
        string bootstrapDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "bootstraplauncher", "1.1.2");
        Directory.CreateDirectory(bootstrapDir);
        await File.WriteAllBytesAsync(Path.Combine(bootstrapDir, "bootstraplauncher-1.1.2.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string secureDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "securejarhandler", "2.1.10");
        Directory.CreateDirectory(secureDir);
        await File.WriteAllBytesAsync(Path.Combine(secureDir, "securejarhandler-2.1.10.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string fmlLoaderDir = Path.Combine(mcDir, "libraries", "net", "minecraftforge", "fmlloader", "1.20.1-47.3.0");
        Directory.CreateDirectory(fmlLoaderDir);
        await File.WriteAllBytesAsync(Path.Combine(fmlLoaderDir, "fmlloader-1.20.1-47.3.0.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string vanillaDir = Path.Combine(mcDir, "versions", "1.20.1");
        Directory.CreateDirectory(vanillaDir);
        await File.WriteAllBytesAsync(Path.Combine(vanillaDir, "1.20.1.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        await File.WriteAllTextAsync(versionJsonPath, MakeForgeJson());

        try
        {
            var reporter = new ListReporter();
            var context = new CommandContext(System.Threading.CancellationToken.None, reporter);
            context.Set("GameProvider.Minecraft", new MinecraftGameQueryProvider());

            var stepConfig = System.Text.Json.JsonDocument.Parse($@"{{
                ""provider"": ""Minecraft"",
                ""version"": ""1.20.1-forge-47.3.0"",
                ""installDir"": ""{Path.Combine(tempDir, ".minecraft").Replace("\\", "\\\\")}""
            }}").RootElement;

            var command = new Vantuz.Plugins.Game.GameInstallerCommand();
            var result = await command.ExecuteAsync(context, stepConfig);

            Assert.True(result.Success);
            Assert.Contains("СѓР¶Рµ СѓСЃС‚Р°РЅРѕРІР»РµРЅР°", reporter.Logs[^1]); // "Р’РµСЂСЃРёСЏ ... СѓР¶Рµ СѓСЃС‚Р°РЅРѕРІР»РµРЅР°, РїСЂРѕРїСѓСЃРє СѓСЃС‚Р°РЅРѕРІРєРё."
            Assert.True(context.Get<bool>("InstallSkipped"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When Forge is not yet installed, InstallVersionAsync calls ForgeInstaller.Install
    /// then calls launcher.InstallAsync to download libraries, then VerifyForgeLibraries.
    /// F_doc: This is the critical path that was missing in Phases 16-17, causing missing libraries.
    /// </summary>
    [Fact]
    public async Task InstallVersionAsync_ForgePath_CallsLibraryResolver()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        string versionsDir = Path.Combine(mcDir, "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");

        // Create all critical libraries + vanilla JAR so VerifyForgeLibraries passes
        string bootstrapDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "bootstraplauncher", "1.1.2");
        Directory.CreateDirectory(bootstrapDir);
        await File.WriteAllBytesAsync(Path.Combine(bootstrapDir, "bootstraplauncher-1.1.2.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string secureDir = Path.Combine(mcDir, "libraries", "cpw", "mods", "securejarhandler", "2.1.10");
        Directory.CreateDirectory(secureDir);
        await File.WriteAllBytesAsync(Path.Combine(secureDir, "securejarhandler-2.1.10.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string fmlLoaderDir = Path.Combine(mcDir, "libraries", "net", "minecraftforge", "fmlloader", "1.20.1-47.3.0");
        Directory.CreateDirectory(fmlLoaderDir);
        await File.WriteAllBytesAsync(Path.Combine(fmlLoaderDir, "fmlloader-1.20.1-47.3.0.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        string vanillaDir = Path.Combine(mcDir, "versions", "1.20.1");
        Directory.CreateDirectory(vanillaDir);
        await File.WriteAllBytesAsync(Path.Combine(vanillaDir, "1.20.1.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        await File.WriteAllTextAsync(versionJsonPath, MakeForgeJson());

        var provider = new MinecraftGameCommandProvider();
        string? libraryInstallerVersion = null;

        // Fake ForgeInstaller: returns the version name without doing network I/O
        provider.ForgeInstallOverride = (mcVersion, forgeVersion, options) => Task.FromResult("1.20.1-forge-47.3.0");

        // Fake LibraryInstaller: records the call and returns immediately
        provider.LibraryInstaller = (launcher, installedName) =>
        {
            libraryInstallerVersion = installedName;
            return Task.CompletedTask;
        };

        try
        {
            var reporter = new ListReporter();
            var result = await provider.InstallVersionAsync("1.20.1-forge-47.3.0", mcDir, reporter, default, TimeSpan.FromSeconds(30));

            Assert.True(result.Success, $"Expected Success=true but got: {result.ErrorMessage}");
            Assert.NotNull(libraryInstallerVersion);
            Assert.Equal("1.20.1-forge-47.3.0", libraryInstallerVersion);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private class ListReporter : IStatusReporter
    {
        /// F_doc: {Logs returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Logs behavior
        public List<string> Logs { get; } = new();
        /// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior
        public void ReportState(string message) => Logs.Add(message);
        /// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior
        public void ReportProgress(string taskName, double percentage) => Logs.Add($"[{taskName}] {percentage:F1}%");
    }
}
