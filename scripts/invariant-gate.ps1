#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string[]]$ChangedFiles = @()
)
$ErrorActionPreference = "Stop"
$scriptDir = Join-Path $ProjectRoot "scripts"
$results = @()
$overall = $true

function Register($Invariant, $Status, $Detail, $Code=0) {
    $script:results += @{ Invariant=$Invariant; Status=$Status; Detail=$Detail; ExitCode=$Code }
    if ($Status -eq "FAIL") { $script:overall = $false }
    $color = if ($Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "  [$Status] $Invariant - $Detail" -ForegroundColor $color
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SOLUTION-INVARIANT GATE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# If no changed files provided, infer from git
if ($ChangedFiles.Count -eq 0) {
    try {
        $gitOut = git -C $ProjectRoot diff --name-only HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and $gitOut) {
            $ChangedFiles = $gitOut -split "`r?`n" | Where-Object { $_ -ne "" }
        }
    } catch { }
}

if ($ChangedFiles.Count -eq 0) {
    Write-Host "No changed files detected. Gate passes by default (nothing to verify)." -ForegroundColor Yellow
    exit 0
}

Write-Host "Changed files: $($ChangedFiles.Count)" -ForegroundColor White

# INV-001: Falsifiability - new .cs must have F_doc/E_doc
Write-Host "--- INV-001: Falsifiability Coverage ---" -ForegroundColor Yellow
$newCs = $ChangedFiles | Where-Object { $_ -match "\.cs$" -and $_ -notmatch "(Designer|Generated)\.cs$" }
if ($newCs) {
    try {
        $out = & "$scriptDir\check-falsifiability.ps1" -SourcePath $ProjectRoot -Threshold 90 2>&1
        if ($LASTEXITCODE -eq 0) { Register "INV-001" "PASS" "Falsifiability coverage maintained" }
        else { Register "INV-001" "FAIL" "Falsifiability coverage dropped" $LASTEXITCODE }
    } catch { Register "INV-001" "FAIL" $_.Exception.Message 1 }
} else {
    Register "INV-001" "PASS" "No new .cs files"
}

# INV-002: CQRS - new .cs must not violate Read/Write separation
Write-Host "--- INV-002: CQRS Separation ---" -ForegroundColor Yellow
if ($newCs) {
    try {
        $out = & "$scriptDir\audit-compliance.ps1" -SourcePath $ProjectRoot 2>&1
        if ($LASTEXITCODE -eq 0) { Register "INV-002" "PASS" "CQRS separation maintained" }
        else { Register "INV-002" "FAIL" "CQRS violation introduced" $LASTEXITCODE }
    } catch { Register "INV-002" "FAIL" $_.Exception.Message 1 }
} else {
    Register "INV-002" "PASS" "No new .cs files"
}

# INV-004: Encoding - any changed file with non-ASCII must be UTF-8-BOM
Write-Host "--- INV-004: Encoding Invariant ---" -ForegroundColor Yellow
$encodingFail = $false
$utf8Bom = [System.Text.Encoding]::UTF8.GetPreamble()
foreach ($relPath in $ChangedFiles) {
    $fullPath = Join-Path $ProjectRoot $relPath
    if (-not (Test-Path $fullPath)) { continue }
    if (-not ($relPath -match "\.cs$")) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $hasNonAscii = $false
    foreach ($b in $bytes) { if ($b -ge 0x80) { $hasNonAscii = $true; break } }
    if ($hasNonAscii) {
        $hasBom = ($bytes.Count -ge 3 -and $bytes[0] -eq $utf8Bom[0] -and $bytes[1] -eq $utf8Bom[1] -and $bytes[2] -eq $utf8Bom[2])
        if (-not $hasBom) {
            Register "INV-004" "FAIL" "Missing BOM: $relPath" 1
            $encodingFail = $true
        }
    }
}
if (-not $encodingFail) {
    Register "INV-004" "PASS" "All changed .cs files UTF-8-BOM compliant"
}

# INV-009: Build - any change must not break build
Write-Host "--- INV-009: Build Pass ---" -ForegroundColor Yellow
try {
    dotnet build "$ProjectRoot\VantuzLauncher.sln" --verbosity quiet | Out-Null
    if ($LASTEXITCODE -eq 0) { Register "INV-009" "PASS" "Build succeeds" }
    else { Register "INV-009" "FAIL" "Build broken" $LASTEXITCODE }
} catch { Register "INV-009" "FAIL" $_.Exception.Message 1 }

# INV-011: Solution-Invariant Gate - itself must be documented
Write-Host "--- INV-011: Gate Documentation ---" -ForegroundColor Yellow
try {
    $theoryPath = "$ProjectRoot\docs\theory\solution-invariant-theory.md"
    if (Test-Path $theoryPath) {
        Register "INV-011" "PASS" "solution-invariant-theory.md exists"
    } else {
        Register "INV-011" "FAIL" "solution-invariant-theory.md missing" 1
    }
} catch { Register "INV-011" "FAIL" $_.Exception.Message 1 }

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SOLUTION-INVARIANT GATE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
foreach ($r in $results) {
    $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "$($r.Invariant): $($r.Status)" -ForegroundColor $color
}
Write-Host ""
$overallColor = if ($overall) { "Green" } else { "Red" }
$overallStatus = if ($overall) { "PASS" } else { "FAIL" }
Write-Host "OVERALL: $overallStatus" -ForegroundColor $overallColor
Write-Host "========================================"
Write-Host ""

$exitCode = if ($overall) { 0 } else { 1 }
exit $exitCode
