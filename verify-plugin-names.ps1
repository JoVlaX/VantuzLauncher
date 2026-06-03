<#
.SYNOPSIS
    Build-time verification: ensures every pipeline pluginName in boot.json
    resolves to a discovered plugin class Name property.
    Per INVARIANT_THEORY.md §1.2 Measurability.
#>
param(
    [Parameter(Mandatory)]
    [string]$BootJsonPath,

    [Parameter(Mandatory)]
    [string]$PluginsDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BootJsonPath)) {
    Write-Error "boot.json not found: $BootJsonPath"
    exit 1
}
if (-not (Test-Path $PluginsDir)) {
    Write-Error "Plugins directory not found: $PluginsDir"
    exit 1
}

$boot = Get-Content $BootJsonPath -Raw | ConvertFrom-Json
$expectedNames = $boot.pipeline | ForEach-Object { $_.pluginName }

# Pre-load Vantuz.Core so plugin assemblies resolve their interface references
$coreDll = Join-Path (Split-Path $PluginsDir -Parent) "Vantuz.Core.dll"
if (Test-Path $coreDll) {
    try { [void][System.Reflection.Assembly]::LoadFrom($coreDll) } catch {}
}

# Also ensure output directory is in the load context for transitive deps
$hostDir = Split-Path $PluginsDir -Parent
$hostDeps = Get-ChildItem -Path $hostDir -Filter "*.dll" -ErrorAction SilentlyContinue
foreach ($dep in $hostDeps) {
    try { [void][System.Reflection.Assembly]::LoadFrom($dep.FullName) } catch {}
}

$dlls = Get-ChildItem -Path $PluginsDir -Filter "*.dll" | Select-Object -ExpandProperty FullName
$discoveredNames = [System.Collections.Generic.List[string]]::new()
$discoveredMap = @{}  # name -> dll

foreach ($dll in $dlls) {
    try {
        $asm = [System.Reflection.Assembly]::LoadFrom($dll)
        foreach ($type in $asm.GetTypes()) {
            # Must implement ICommandPlugin or IQueryPlugin
            $iface = $type.GetInterfaces() | Where-Object {
                $_.Name -eq "ICommandPlugin" -or $_.Name -eq "IQueryPlugin"
            }
            if (-not $iface) { continue }

            $prop = $type.GetProperty("Name", [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance)
            if (-not $prop -or $prop.PropertyType -ne [string]) { continue }

            $ctor = $type.GetConstructor([Type[]]@())
            if (-not $ctor) { continue }

            $instance = $ctor.Invoke($null)
            $name = $prop.GetValue($instance)
            if ($name) {
                [void]$discoveredNames.Add($name)
                $discoveredMap[$name] = [IO.Path]::GetFileName($dll)
            }
        }
    } catch {
        # Skip assemblies that fail to load (e.g., native deps)
    }
}

# Auth.TestAuthCommand is headless-only; GUI pipeline may omit it. That's OK.
# We only check names that ARE in the pipeline against discovered plugins.
$mismatches = $expectedNames | Where-Object { $_ -notin $discoveredNames }

if ($mismatches.Count -gt 0) {
    foreach ($m in $mismatches) {
        Write-Error "[PLUGIN NAME MISMATCH] Pipeline references '$m' but no plugin class reports that Name."
    }
    exit 1
}

Write-Host "[PASS] All $($expectedNames.Count) pipeline pluginNames verified against $($discoveredNames.Count) discovered plugin classes."
exit 0
