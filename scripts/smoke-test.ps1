# Armatura Smoke Test
# Local verification - no CI/CD infrastructure required
# Per INVARIANT_THEORY.md §3.4: Empirical falsifiability

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Test-FileExists {
    param([string]$path, [string]$description)
    if (Test-Path $path) {
        Write-Host "  ✓ $description" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  ✗ $description (missing: $path)" -ForegroundColor Red
        return $false
    }
}

function Test-JsonValid {
    param([string]$path, [string]$description)
    try {
        Get-Content $path -Raw | ConvertFrom-Json | Out-Null
        Write-Host "  ✓ $description" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  ✗ $description (invalid JSON)" -ForegroundColor Red
        return $false
    }
}

Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Armatura Smoke Test" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host

$baseDir = "c:\000\projects\compositum\bin\Debug\net8.0-windows\win-x64"
$allPassed = $true

# Test 1: Required executables
Write-Host "[TEST] Required Executables" -ForegroundColor Yellow
$allPassed = (Test-FileExists "$baseDir\VantuzLauncher.exe" "VantuzLauncher.exe") -and $allPassed
Write-Host

# Test 2: Required manifests
Write-Host "[TEST] Boot Manifests" -ForegroundColor Yellow
$allPassed = (Test-JsonValid "$baseDir\boot.json" "boot.json") -and $allPassed
$allPassed = (Test-JsonValid "$baseDir\boot.gui.json" "boot.gui.json") -and $allPassed
$allPassed = (Test-JsonValid "$baseDir\boot.test.json" "boot.test.json") -and $allPassed
Write-Host

# Test 3: Required plugins
Write-Host "[TEST] Plugin DLLs" -ForegroundColor Yellow
$pluginsDir = "$baseDir\plugins"
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Plugins.Auth.dll" "Auth Plugin") -and $allPassed
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Plugins.Net.dll" "Net Plugin") -and $allPassed
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Plugins.OS.dll" "OS Plugin") -and $allPassed
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Plugins.Game.dll" "Game Plugin") -and $allPassed
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Plugins.Minecraft.dll" "Minecraft Plugin") -and $allPassed
$allPassed = (Test-FileExists "$pluginsDir\Vantuz.Products.MinecraftLauncher.GUI.dll" "GUI Plugin") -and $allPassed
Write-Host

# Test 4: Dev markers (optional in production)
Write-Host "[TEST] Development Environment" -ForegroundColor Yellow
if (Test-Path "$baseDir\.dev") {
    Write-Host "  ⚠ .dev marker present (debug mode)" -ForegroundColor Yellow
} else {
    Write-Host "  ✓ Production mode (no .dev marker)" -ForegroundColor Green
}
Write-Host

# Final result
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  Smoke Test PASSED ✓" -ForegroundColor Green
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "  Smoke Test FAILED ✗" -ForegroundColor Red
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    exit 1
}
