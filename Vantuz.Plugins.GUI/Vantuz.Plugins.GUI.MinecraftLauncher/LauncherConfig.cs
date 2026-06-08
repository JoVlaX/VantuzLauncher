namespace Vantuz.Plugins.GUI.MinecraftLauncher;

/// <summary>
/// Configuration model for Minecraft Launcher.
/// Per INVARIANT_THEORY.md В§11.2: Explicit configuration externalized from code.
/// F_doc: {config property missing or has invalid value (negative RAM, empty username)}
/// E_doc: JSON deserialization test with malformed config payload
/// </summary>
public class LauncherConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public int RamMb { get; set; } = 4096;
}
