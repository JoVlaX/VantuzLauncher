with open(r'c:\000\projects\compositum\VantuzLauncher.csproj', 'r', encoding='utf-8') as f:
    content = f.read()
insert = """    <ProjectReference Include="Vantuz.Plugins.Auth\Vantuz.Plugins.Auth.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
    <ProjectReference Include="Vantuz.Plugins.Net\Vantuz.Plugins.Net.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
    <ProjectReference Include="Vantuz.Plugins.OS\Vantuz.Plugins.OS.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
    <ProjectReference Include="Vantuz.Plugins.Game\Vantuz.Plugins.Game.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
    <ProjectReference Include="Vantuz.Plugins.Minecraft\Vantuz.Plugins.Minecraft.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
"""
old = """    <ProjectReference Include="Vantuz.Plugins.Test\Vantuz.Plugins.Test.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>"""
new = """    <ProjectReference Include="Vantuz.Plugins.Test\Vantuz.Plugins.Test.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <Private>false</Private>
    </ProjectReference>
""" + insert + "  </ItemGroup>"
content = content.replace(old, new)
with open(r'c:\000\projects\compositum\VantuzLauncher.csproj', 'w', encoding='utf-8') as f:
    f.write(content)
print('Done')
