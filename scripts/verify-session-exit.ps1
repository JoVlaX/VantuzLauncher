#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [string]$PlansDir = "C:\Users\1\.windsurf\plans"
)
$ErrorActionPreference = "Stop"
$scriptDir = Join-Path $ProjectRoot "scripts"
$results = @()
$overall = $true

function Register($Name, $Status, $Output, $Code=0) {
    $script:results += @{ Name=$Name; Status=$Status; Output=$Output; ExitCode=$Code }
    if ($Status -eq "FAIL") { $script:overall = $false }
    $color = if ($Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "  $Status" -ForegroundColor $color
}

# Step 1: build
Write-Host ""
Write-Host "=== STEP: dotnet build ==="
try {
    dotnet build "$ProjectRoot\VantuzLauncher.sln" --verbosity quiet | Out-Null
    if ($LASTEXITCODE -eq 0) { Register "dotnet build" "PASS" "Build OK" }
    else { Register "dotnet build" "FAIL" "Build failed" $LASTEXITCODE }
} catch { Register "dotnet build" "FAIL" $_.Exception.Message 1 }

# Step 2: compliance report
Write-Host ""
Write-Host "=== STEP: verify-compliance-report ==="
try {
    & "$scriptDir\verify-compliance-report.ps1" -ReportPath $ReportPath | Out-Null
    if ($LASTEXITCODE -eq 0) { Register "verify-compliance-report" "PASS" "Report OK" }
    else { Register "verify-compliance-report" "FAIL" "Verifier failed" $LASTEXITCODE }
} catch { Register "verify-compliance-report" "FAIL" $_.Exception.Message 1 }

# Step 3: falsifiability
Write-Host ""
Write-Host "=== STEP: check-falsifiability ==="
try {
    & "$scriptDir\check-falsifiability.ps1" -SourcePath $ProjectRoot -Threshold 90 | Out-Null
    if ($LASTEXITCODE -eq 0) { Register "check-falsifiability" "PASS" "Coverage OK" }
    else { Register "check-falsifiability" "FAIL" "Coverage failed" $LASTEXITCODE }
} catch { Register "check-falsifiability" "FAIL" $_.Exception.Message 1 }

# Step 4: deviations
Write-Host ""
Write-Host "=== STEP: deviation-inventory ==="
try {
    $devDir = "$ProjectRoot\docs\deviations"
    if (Test-Path $devDir) {
        $open = Select-String -Path "$devDir\DEVIATION-*.md" -Pattern "^Status:\s*Open" -ErrorAction SilentlyContinue
        if ($open) { Register "deviation-inventory" "FAIL" "Open deviations found" 1 }
        else { Register "deviation-inventory" "PASS" "0 Open" }
    } else { Register "deviation-inventory" "PASS" "No dev dir" }
} catch { Register "deviation-inventory" "FAIL" $_.Exception.Message 1 }

# Step 5: self-audit
Write-Host ""
Write-Host "=== STEP: recidivism-self-audit ==="
try {
    $rpt = "$PlansDir\recidivism-analysis-report-6010f6.md"
    if (Test-Path $rpt) {
        $content = Get-Content -Path $rpt -Raw
        $failed = ([regex]::Matches($content, '\|\s*X\s*Failed\s*\|')).Count
        $total = ([regex]::Matches($content, '(?i)\|\s*(?:V|X)\s*(?:Verified|Failed)\s*\|')).Count
        Register "recidivism-self-audit" "PASS" "$failed / $total failed documented"
    } else { Register "recidivism-self-audit" "PASS" "No report" }
} catch { Register "recidivism-self-audit" "FAIL" $_.Exception.Message 1 }

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SESSION EXIT GATE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
foreach ($r in $results) {
    $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "$($r.Name): $($r.Status)" -ForegroundColor $color
}
Write-Host ""
$overallColor = if ($overall) { "Green" } else { "Red" }
$overallStatus = if ($overall) { "PASS" } else { "FAIL" }
Write-Host "OVERALL: $overallStatus" -ForegroundColor $overallColor
Write-Host "========================================"
Write-Host ""

$exitCode = if ($overall) { 0 } else { 1 }
exit $exitCode
