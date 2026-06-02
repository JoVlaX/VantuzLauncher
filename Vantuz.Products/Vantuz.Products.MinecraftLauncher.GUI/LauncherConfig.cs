namespace Vantuz.Products.MinecraftLauncher.GUI;

/// <summary>
/// Configuration model for Minecraft Launcher.
/// Per INVARIANT_THEORY.md §11.2: Explicit configuration externalized from code.
/// </summary>
public class LauncherConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public int RamMb { get; set; } = 4096;
}
