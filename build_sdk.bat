@echo off
cd /d C:\000\projects\compositum
dotnet build Armatura.Core.Sdk\Armatura.Core.Sdk.csproj
dotnet build Vantuz.Core\Vantuz.Core.csproj
dotnet build Vantuz.Host\Vantuz.Host.csproj
dotnet build Vantuz.Plugins.Game\Vantuz.Plugins.Game.csproj
dotnet build Vantuz.Plugins.Net\Vantuz.Plugins.Net.csproj
dotnet build Vantuz.Plugins.OS\Vantuz.Plugins.OS.csproj
dotnet build Vantuz.Plugins.Auth\Vantuz.Plugins.Auth.csproj
dotnet build Vantuz.Plugins.Minecraft\Vantuz.Plugins.Minecraft.csproj
dotnet build Vantuz.Builder\Vantuz.Builder.csproj
dotnet build VantuzLauncher.csproj
