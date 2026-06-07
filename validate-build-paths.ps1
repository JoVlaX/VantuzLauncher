#Requires -Version 5.1
<#
.SYNOPSIS
    Atomic validation pipeline for both dotnet build and dotnet run --project paths.

.DESCRIPTION
    Per INVARIANT_THEORY.md §1.2 (Measurability) and §4.1 (Falsifiability).
    Prevents regressions where code passes solution-level builds but fails on
    single-project runs (e.g. plugin copy order bugs).

.PARAMETER AssertAll
    Run all assertions (default).

.PARAMETER AssertCleanBuild
    Only validate dotnet build VantuzLauncher.sln -c Release after clean.

.PARAMETER AssertDotNetRun
    Only validate dotnet run --project VantuzLauncher.csproj --headless.

.PARAMETER AssertTestResult
    Only validate test-result.json content.

.PARAMETER AssertPluginsCopied
    Only validate plugin DLLs exist in output/plugins.

.PARAMETER AssertBootJsonIntegrity
    Only validate boot.json hashes match actual plugin DLLs.

.PARAMETER AssertPipelineNames
    Only validate that every boot.json pipeline pluginName resolves to a discovered plugin class.

.PARAMETER AssertGuiPipeline
    Only validate GUI pipeline via dotnet test (GuiPipelinePositiveVerificationTests).

.EXAMPLE
    .\validate-build-paths.ps1

.EXAMPLE
    .\validate-build-paths.ps1 -AssertDotNetRun
#>
[CmdletBinding()]
param(
    [switch]$AssertAll,
    [switch]$AssertCleanBuild,
    [switch]$AssertDotNetRun,
    [switch]$AssertTestResult,
    [switch]$AssertPluginsCopied,
    [switch]$AssertBootJsonIntegrity,
    [switch]$AssertPipelineNames,
    [switch]$AssertGuiPipeline
)

# ============================================
# CONFIGURATION (Nomadic: relative paths)
# ============================================
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$slnPath   = Join-Path $scriptDir "VantuzLauncher.sln"
$projPath  = Join-Path $scriptDir "VantuzLauncher.csproj"

# Output paths (Release preferred, fallback to Debug)
$releaseDir = Join-Path $scriptDir "bin\Release\net8.0-windows"
$debugDir   = Join-Path $scriptDir "bin\Debug\net8.0-windows"
$outputDir  = if (Test-Path $releaseDir) { $releaseDir } else { $debugDir }

$testResultPath = Join-Path $scriptDir "test-result.json"
$bootJsonPath   = Join-Path $outputDir "boot.json"
$pluginsDir     = Join-Path $outputDir "plugins"

# Expected plugin DLLs per project structure
$expectedPlugins = @(
    "Vantuz.Plugins.Auth.dll"
    "Vantuz.Plugins.Net.dll"
    "Vantuz.Plugins.OS.dll"
    "Vantuz.Plugins.Game.dll"
    "Vantuz.Plugins.Minecraft.dll"
)

$colors = @{
    Pass = "Green"
    Fail = "Red"
    Info = "Cyan"
}

# Default to AssertAll if no specific flag given
if (-not ($AssertCleanBuild -or $AssertDotNetRun -or $AssertTestResult -or $AssertPluginsCopied -or $AssertBootJsonIntegrity -or $AssertPipelineNames -or $AssertGuiPipeline)) {
    $AssertAll = $true
}

# ============================================
# HELPER FUNCTIONS
# ============================================

function Write-Assertion {
    param([string]$Name, [bool]$Passed, [string]$Message)
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color  = if ($Passed) { $colors.Pass } else { $colors.Fail }
    Write-Host "[$status] $Name`: $Message" -ForegroundColor $color
    return $Passed
}

function Invoke-CleanBuild {
    Write-Host "`n[Assert-CleanBuild] Cleaning and building solution..." -ForegroundColor $colors.Info

    # Deterministic clean via dotnet clean (do NOT manually wipe bin/obj recursively;
    # Armatura.Core.Sdk analyzer DLL must be rebuilt by MSBuild dependency resolution)
    & dotnet clean $slnPath --verbosity quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        return @{ Passed = $false; Message = "dotnet clean failed" }
    }

    & dotnet build $slnPath -c Release --verbosity minimal 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    if ($LASTEXITCODE -ne 0) {
        return @{ Passed = $false; Message = "dotnet build exited with code $LASTEXITCODE" }
    }

    return @{ Passed = $true; Message = "Build succeeded" }
}

function Invoke-DotNetRun {
    Write-Host "`n[Assert-DotNetRun] Running dotnet run --project ..." -ForegroundColor $colors.Info

    $arguments = @(
        "run",
        "--project", $projPath,
        "--",
        "--headless",
        "--workspace=$scriptDir",
        "--boot=boot.headless.json"
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = $arguments -join " "
    $psi.WorkingDirectory = $scriptDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    $out = New-Object System.Text.StringBuilder
    $err = New-Object System.Text.StringBuilder
    Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action { $out.AppendLine($EventArgs.Data) } | Out-Null
    Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived  -Action { $err.AppendLine($EventArgs.Data) } | Out-Null

    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    $timeoutMs = 120 * 1000
    $completed = $process.WaitForExit($timeoutMs)
    if (-not $completed) {
        $process.Kill()
        return @{ Passed = $false; Message = "dotnet run timed out after 120s" }
    }
    $process.WaitForExit()

    $exitCode = $process.ExitCode
    if ($exitCode -ne 0) {
        $errText = $err.ToString()
        return @{ Passed = $false; Message = "dotnet run exited with code $exitCode. StdErr: $errText" }
    }

    return @{ Passed = $true; Message = "dotnet run succeeded" }
}

function Invoke-TestResultCheck {
    Write-Host "`n[Assert-TestResult] Validating test-result.json..." -ForegroundColor $colors.Info

    if (-not (Test-Path $testResultPath)) {
        return @{ Passed = $false; Message = "test-result.json not found" }
    }

    try {
        $json = Get-Content $testResultPath -Raw | ConvertFrom-Json
    }
    catch {
        return @{ Passed = $false; Message = "Failed to parse test-result.json: $_" }
    }

    if ($json.success -ne $true) {
        return @{ Passed = $false; Message = "test-result.json success = $($json.success)" }
    }

    if ($json.status -ne "success") {
        return @{ Passed = $false; Message = "test-result.json status = $($json.status)" }
    }

    return @{ Passed = $true; Message = "Test result valid (success=true, status=success)" }
}

function Invoke-PluginsCopiedCheck {
    Write-Host "`n[Assert-PluginsCopied] Checking plugin DLLs in $pluginsDir ..." -ForegroundColor $colors.Info

    if (-not (Test-Path $pluginsDir)) {
        return @{ Passed = $false; Message = "plugins directory not found: $pluginsDir" }
    }

    $missing = @()
    foreach ($dll in $expectedPlugins) {
        $path = Join-Path $pluginsDir $dll
        if (-not (Test-Path $path)) {
            $missing += $dll
        }
    }

    if ($missing.Count -gt 0) {
        return @{ Passed = $false; Message = "Missing plugins: $($missing -join ', ')" }
    }

    return @{ Passed = $true; Message = "All $($expectedPlugins.Count) plugin DLLs present" }
}

function Invoke-PipelineNamesCheck {
    Write-Host "`n[Assert-PipelineNames] Checking pipeline pluginNames against discovered plugin classes..." -ForegroundColor $colors.Info

    if (-not (Test-Path $outputDir)) {
        return @{ Passed = $false; Message = "output directory not found: $outputDir" }
    }
    if (-not (Test-Path $pluginsDir)) {
        return @{ Passed = $false; Message = "plugins directory not found: $pluginsDir" }
    }

    $builderProj = Join-Path $scriptDir "Vantuz.Builder\Vantuz.Builder.csproj"
    if (-not (Test-Path $builderProj)) {
        return @{ Passed = $false; Message = "Vantuz.Builder.csproj not found at $builderProj" }
    }

    & dotnet run --project $builderProj -- verify-dir $outputDir $pluginsDir 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    if ($LASTEXITCODE -ne 0) {
        return @{ Passed = $false; Message = "Plugin name mismatch detected in one or more manifests (see errors above)" }
    }

    return @{ Passed = $true; Message = "All pipeline pluginNames verified in all manifests against plugin DLLs" }
}

function Invoke-GuiPipelineCheck {
    Write-Host "`n[Assert-GuiPipeline] Running GUI pipeline resolution tests..." -ForegroundColor $colors.Info

    $testProj = Join-Path $scriptDir "Vantuz.Core.Tests\Vantuz.Core.Tests.csproj"
    if (-not (Test-Path $testProj)) {
        return @{ Passed = $false; Message = "Vantuz.Core.Tests.csproj not found at $testProj" }
    }

    # Build test project first (ensures boot.json and plugins exist)
    & dotnet build $testProj --verbosity quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        return @{ Passed = $false; Message = "Failed to build Vantuz.Core.Tests" }
    }

    & dotnet test $testProj --filter "FullyQualifiedName~GuiPipelinePositiveVerificationTests" --no-build --verbosity quiet 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    if ($LASTEXITCODE -ne 0) {
        return @{ Passed = $false; Message = "GUI pipeline resolution tests failed (see output above)" }
    }

    return @{ Passed = $true; Message = "GUI pipeline resolution tests passed (13 steps resolve, no 'Plugin not found' crash)" }
}

function Invoke-BootJsonIntegrityCheck {
    Write-Host "`n[Assert-BootJsonIntegrity] Checking boot.json hashes..." -ForegroundColor $colors.Info

    if (-not (Test-Path $bootJsonPath)) {
        return @{ Passed = $false; Message = "boot.json not found: $bootJsonPath" }
    }

    try {
        $boot = Get-Content $bootJsonPath -Raw | ConvertFrom-Json
    }
    catch {
        return @{ Passed = $false; Message = "Failed to parse boot.json: $_" }
    }

    # Verify each expected plugin hash matches actual file hash
    $mismatches = @()
    foreach ($dll in $expectedPlugins) {
        $dllPath = Join-Path $pluginsDir $dll
        if (-not (Test-Path $dllPath)) {
            $mismatches += "$dll (missing)"
            continue
        }
        $actualHash = (Get-FileHash $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = $boot.plugins.$dll
        if (-not $expectedHash) {
            $mismatches += "$dll (not in boot.json)"
            continue
        }
        if ($expectedHash -ne $actualHash) {
            $mismatches += "$dll (hash mismatch: boot=$expectedHash, actual=$actualHash)"
        }
    }

    if ($mismatches.Count -gt 0) {
        return @{ Passed = $false; Message = "Hash mismatches: $($mismatches -join '; ')" }
    }

    return @{ Passed = $true; Message = "boot.json integrity verified" }
}

# ============================================
# MAIN EXECUTION
# ============================================

$results = @()
$allPassed = $true

if ($AssertCleanBuild -or $AssertAll) {
    $r = Invoke-CleanBuild
    $results += [PSCustomObject]@{ Assertion = "CleanBuild"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertDotNetRun -or $AssertAll) {
    $r = Invoke-DotNetRun
    $results += [PSCustomObject]@{ Assertion = "DotNetRun"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertTestResult -or $AssertAll) {
    $r = Invoke-TestResultCheck
    $results += [PSCustomObject]@{ Assertion = "TestResult"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertPluginsCopied -or $AssertAll) {
    $r = Invoke-PluginsCopiedCheck
    $results += [PSCustomObject]@{ Assertion = "PluginsCopied"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertBootJsonIntegrity -or $AssertAll) {
    $r = Invoke-BootJsonIntegrityCheck
    $results += [PSCustomObject]@{ Assertion = "BootJsonIntegrity"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertPipelineNames -or $AssertAll) {
    $r = Invoke-PipelineNamesCheck
    $results += [PSCustomObject]@{ Assertion = "PipelineNames"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

if ($AssertGuiPipeline -or $AssertAll) {
    $r = Invoke-GuiPipelineCheck
    $results += [PSCustomObject]@{ Assertion = "GuiPipeline"; Passed = $r.Passed; Message = $r.Message }
    if (-not $r.Passed) { $allPassed = $false }
}

# Summary table
Write-Host "`n============================================" -ForegroundColor $colors.Info
Write-Host "VALIDATION SUMMARY" -ForegroundColor $colors.Info
Write-Host "============================================" -ForegroundColor $colors.Info
foreach ($res in $results) {
    Write-Assertion -Name $res.Assertion -Passed $res.Passed -Message $res.Message
}

if ($allPassed) {
    Write-Host "`nALL ASSERTIONS PASSED" -ForegroundColor $colors.Pass
    exit 0
}
else {
    Write-Host "`nASSERTIONS FAILED" -ForegroundColor $colors.Fail
    exit 1
}
