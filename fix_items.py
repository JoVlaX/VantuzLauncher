with open(r'c:\000\projects\compositum\VantuzLauncher.csproj', 'r', encoding='utf-8') as f:
    content = f.read()

old_itemgroup = '''  <ItemGroup>
    <PluginNetFiles Include="$(ProjectDir)Vantuz.Plugins.Net\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    <PluginAuthFiles Include="$(ProjectDir)Vantuz.Plugins.Auth\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    <PluginOSFiles Include="$(ProjectDir)Vantuz.Plugins.OS\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    <PluginGameFiles Include="$(ProjectDir)Vantuz.Plugins.Game\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    <PluginMinecraftFiles Include="$(ProjectDir)Vantuz.Plugins.Minecraft\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    <!-- DEVIATION-001: Products folder for GUI plugin pending migration to Vantuz.Plugins.GUI -->
    <!-- Note: WPF projects use net8.0-windows target framework -->
    <PluginGUIFiles Include="$(ProjectDir)Vantuz.Products\\Vantuz.Products.MinecraftLauncher.GUI\\bin\\$(Configuration)\\net8.0-windows\\**\\*.*" />
    <!-- Headless test plugin per INVARIANT_THEORY.md Measurability -->
    <PluginTestFiles Include="$(ProjectDir)Vantuz.Plugins.Test\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
  </ItemGroup> 
'''

new_target_content = '''  <Target Name="AssembleVantuz" AfterTargets="Build">
    <Message Text="[Vantuz Assembler] Copying plugins to target directory..." Importance="high" />
    <ItemGroup>
      <PluginNetFiles Include="$(ProjectDir)Vantuz.Plugins.Net\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
      <PluginAuthFiles Include="$(ProjectDir)Vantuz.Plugins.Auth\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
      <PluginOSFiles Include="$(ProjectDir)Vantuz.Plugins.OS\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
      <PluginGameFiles Include="$(ProjectDir)Vantuz.Plugins.Game\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
      <PluginMinecraftFiles Include="$(ProjectDir)Vantuz.Plugins.Minecraft\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
      <!-- DEVIATION-001: Products folder for GUI plugin pending migration to Vantuz.Plugins.GUI -->
      <!-- Note: WPF projects use net8.0-windows target framework -->
      <PluginGUIFiles Include="$(ProjectDir)Vantuz.Products\\Vantuz.Products.MinecraftLauncher.GUI\\bin\\$(Configuration)\\net8.0-windows\\**\\*.*" />
      <!-- Headless test plugin per INVARIANT_THEORY.md Measurability -->
      <PluginTestFiles Include="$(ProjectDir)Vantuz.Plugins.Test\\bin\\$(Configuration)\\net8.0\\**\\*.*" />
    </ItemGroup>
'''

old_target_start = '''  <Target Name="AssembleVantuz" AfterTargets="Build">
    <Message Text="[Vantuz Assembler] Copying plugins to target directory..." Importance="high" />
'''

content = content.replace(old_itemgroup, '')
content = content.replace(old_target_start, new_target_content)

with open(r'c:\000\projects\compositum\VantuzLauncher.csproj', 'w', encoding='utf-8') as f:
    f.write(content)
print('Done')
