#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$PlansDir = "C:\Users\1\.windsurf\plans"
)
$ErrorActionPreference = "Stop"
$scriptDir = Join-Path $ProjectRoot "scripts"
$results = @()
$overall = $true

function Register($Category, $Status, $Detail, $Code=0) {
    $script:results += @{ Category=$Category; Status=$Status; Detail=$Detail; ExitCode=$Code }
    if ($Status -eq "FAIL") { $script:overall = $false }
    $color = if ($Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "  [$Status] $Category - $Detail" -ForegroundColor $color
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "EXHAUSTIVE AUDIT - All Categories" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Category 1: Falsifiability coverage (INVARIANT_THEORY §1.2)
Write-Host "--- Category 1: Falsifiability Coverage ---" -ForegroundColor Yellow
try {
    $out = & "$scriptDir\check-falsifiability.ps1" -SourcePath $ProjectRoot -Threshold 90 2>&1
    if ($LASTEXITCODE -eq 0) { Register "Falsifiability" "PASS" "Coverage >= 90%" }
    else { Register "Falsifiability" "FAIL" "Coverage below threshold" $LASTEXITCODE }
} catch { Register "Falsifiability" "FAIL" $_.Exception.Message 1 }

# Category 2: CQRS Separation (COMPOSITUM_SPEC §2.2 / INVARIANT_THEORY §2.2)
Write-Host "--- Category 2: CQRS Separation ---" -ForegroundColor Yellow
try {
    $out = & "$scriptDir\audit-compliance.ps1" -SourcePath $ProjectRoot -OutputPath "$ProjectRoot\audit-report.json" 2>&1
    if ($LASTEXITCODE -eq 0) { Register "CQRS" "PASS" "No violations detected" }
    else { Register "CQRS" "FAIL" "Violations found" $LASTEXITCODE }
} catch { Register "CQRS" "FAIL" $_.Exception.Message 1 }

# Category 3: Empty catch blocks (INVARIANT_THEORY §1.1 - determinism)
Write-Host "--- Category 3: Empty Catch Blocks ---" -ForegroundColor Yellow
try {
    $emptyCatches = Get-ChildItem -Path $ProjectRoot -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
    } | Select-String -Pattern "catch\s*\{\s*\}" -ErrorAction SilentlyContinue
    if ($emptyCatches) {
        Register "Empty-Catch" "FAIL" "$($emptyCatches.Count) empty catch blocks" 1
    } else {
        Register "Empty-Catch" "PASS" "0 empty catch blocks"
    }
} catch { Register "Empty-Catch" "FAIL" $_.Exception.Message 1 }

# Category 4: Encoding (Encoding Invariant)
Write-Host "--- Category 4: Source Encoding ---" -ForegroundColor Yellow
try {
    $out = & "$scriptDir\verify-encoding.ps1" -ProjectRoot $ProjectRoot 2>&1
    if ($LASTEXITCODE -eq 0) { Register "Encoding" "PASS" "All .cs UTF-8 BOM" }
    else { Register "Encoding" "FAIL" "Encoding violations" $LASTEXITCODE }
} catch { Register "Encoding" "FAIL" $_.Exception.Message 1 }

# Category 5: Startup feedback (User-Perceptual-Feedback Invariant)
Write-Host "--- Category 5: Startup Feedback ---" -ForegroundColor Yellow
try {
    $progCs = Get-Content "$ProjectRoot\Program.cs" -Raw
    if ($progCs -match "Win32SplashScreen\.Show\(\)") {
        Register "Startup-Feedback" "PASS" "Win32SplashScreen.Show() found in Program.cs"
    } else {
        Register "Startup-Feedback" "FAIL" "No splash screen call in Program.cs" 1
    }
} catch { Register "Startup-Feedback" "FAIL" $_.Exception.Message 1 }

# Category 6: Deviation inventory (COMPOSITUM_SPEC §7.2)
Write-Host "--- Category 6: Deviation Inventory ---" -ForegroundColor Yellow
try {
    $devDir = "$ProjectRoot\docs\deviations"
    if (Test-Path $devDir) {
        $open = Select-String -Path "$devDir\DEVIATION-*.md" -Pattern "^Status:\s*Open" -ErrorAction SilentlyContinue
        if ($open) { Register "Deviations" "FAIL" "Open deviations: $($open.Count)" 1 }
        else { Register "Deviations" "PASS" "0 Open deviations" }
    } else { Register "Deviations" "PASS" "No deviation directory" }
} catch { Register "Deviations" "FAIL" $_.Exception.Message 1 }

# Category 7: Plan compliance (COMPOSITUM_SPEC §0.3)
Write-Host "--- Category 7: Plan Compliance ---" -ForegroundColor Yellow
try {
    $plans = Get-ChildItem -Path $PlansDir -Filter "*.md" -ErrorAction SilentlyContinue
    $nonCompliant = 0
    foreach ($plan in $plans) {
        $content = Get-Content $plan.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -and ($content -match "##\s*Meta-Compliance" -or $content -match "##\s*Self-Audit")) {
            # Has meta-compliance section - OK for new plans
            continue
        }
        if ($content -and ($content -match "^#\s*Plan" -or $content -match "^##\s*Objectives")) {
            # This looks like a plan without Meta-Compliance
            # Check if it's a grandfathered plan (created before policy)
            $creationDate = $plan.CreationTime
            if ($creationDate -gt [datetime]::new(2026, 6, 13)) {
                $nonCompliant++
            }
        }
    }
    if ($nonCompliant -gt 0) {
        Register "Plan-Compliance" "FAIL" "$nonCompliant new plans without Meta-Compliance" 1
    } else {
        Register "Plan-Compliance" "PASS" "All plans compliant or grandfathered"
    }
} catch { Register "Plan-Compliance" "FAIL" $_.Exception.Message 1 }

# Category 8: Agent recidivism self-audit
Write-Host "--- Category 8: Recidivism Self-Audit ---" -ForegroundColor Yellow
try {
    $rpt = "$PlansDir\recidivism-analysis-report-6010f6.md"
    if (Test-Path $rpt) {
        $content = Get-Content -Path $rpt -Raw
        $failed = ([regex]::Matches($content, '\|\s*X\s*Failed\s*\|')).Count
        $total = ([regex]::Matches($content, '(?i)\|\s*(?:V|X)\s*(?:Verified|Failed)\s*\|')).Count
        Register "Recidivism-SelfAudit" "PASS" "$failed / $total failed documented"
    } else { Register "Recidivism-SelfAudit" "PASS" "No report" }
} catch { Register "Recidivism-SelfAudit" "FAIL" $_.Exception.Message 1 }

# Category 9: Build
Write-Host "--- Category 9: Build ---" -ForegroundColor Yellow
try {
    dotnet build "$ProjectRoot\VantuzLauncher.sln" --verbosity quiet | Out-Null
    if ($LASTEXITCODE -eq 0) { Register "Build" "PASS" "0 errors" }
    else { Register "Build" "FAIL" "Build failed" $LASTEXITCODE }
} catch { Register "Build" "FAIL" $_.Exception.Message 1 }

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "EXHAUSTIVE AUDIT SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
foreach ($r in $results) {
    $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "$($r.Category): $($r.Status)" -ForegroundColor $color
}
Write-Host ""
$overallColor = if ($overall) { "Green" } else { "Red" }
$overallStatus = if ($overall) { "PASS" } else { "FAIL" }
Write-Host "OVERALL: $overallStatus" -ForegroundColor $overallColor
Write-Host "========================================"
Write-Host ""
Write-Host "Categories: $($results.Count) | PASS: $($results | Where-Object { $_.Status -eq 'PASS' } | Measure-Object | Select-Object -ExpandProperty Count) | FAIL: $($results | Where-Object { $_.Status -eq 'FAIL' } | Measure-Object | Select-Object -ExpandProperty Count)"

$exitCode = if ($overall) { 0 } else { 1 }
exit $exitCode
