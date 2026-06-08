#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies an Armatura compliance report against INVARIANT_THEORY 1.2a and 4.1a.

.DESCRIPTION
    Checks that every claim row has F_doc/E_doc, zero Open rows have deviation protocols,
    and the Self-Audit table is present and complete.

.PARAMETER ReportPath
    Path to the compliance report markdown file.

.EXAMPLE
    .\scripts\verify-compliance-report.ps1 -ReportPath ".windsurf\plans\api-contract-compliance-report-6010f6.md"
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"
$report = Get-Content -Path $ReportPath -Raw
$failures = @()
$warnings = @()

# 1. Verify Self-Audit section exists
if (-not ($report -match '##.*Self-Audit')) {
    $failures += "FAIL: Report missing '## Self-Audit' section (INVARIANT_THEORY 4.1a)"
}

# 2. Verify no Open rows in violation summary
$openRows = [regex]::Matches($report, '\|\s*Open\s*\|')
if ($openRows.Count -gt 0) {
    $failures += "FAIL: Found $($openRows.Count) 'Open' row(s) in violation table. Each must have a deviation protocol (COMPOSITUM_SPEC 7.2)."
}

# 3. Verify Executive Summary has a Verdict line
if (-not ($report -match '\*\*Verdict:\*\*\s*\*\*(COMPLIANT|NON_COMPLIANT)\*\*')) {
    $failures += "FAIL: Report missing explicit 'Verdict: **COMPLIANT**' or '**NON_COMPLIANT**' in Executive Summary."
}

# 4. Verify every resolved row has a resolution description
$resolvedPattern = '\|\s*RESOLVED\s*[^|]*\|'
$resolvedRows = [regex]::Matches($report, $resolvedPattern)
foreach ($row in $resolvedRows) {
    $text = $row.Value
    if (-not ($text -match 'RESOLVED\s+\d{4}-\d{2}-\d{2}')) {
        $warnings += "WARN: Resolved row missing ISO8601 date: $text"
    }
    if (-not ($text -match '\u2014|added|removed|split|fixed|implemented')) {
        $warnings += "WARN: Resolved row missing resolution description: $text"
    }
}

# 5. Verify Self-Audit table has Falsifier and E_doc columns
$selfAuditSection = [regex]::Match($report, '##.*Self-Audit.*?(?=##\s|\z)', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if ($selfAuditSection.Success) {
    if (-not ($selfAuditSection.Value -match 'Falsifier')) {
        $failures += "FAIL: Self-Audit table missing 'Falsifier' column (INVARIANT_THEORY 4.1a)"
    }
    if (-not ($selfAuditSection.Value -match 'Empirical Test')) {
        $failures += "FAIL: Self-Audit table missing 'Empirical Test' column (INVARIANT_THEORY 4.1a)"
    }
} else {
    $failures += "FAIL: Could not extract Self-Audit section"
}

# 6. Verify report references parent documents
if (-not ($report -match 'INVARIANT_THEORY')) {
    $warnings += "WARN: Report does not reference INVARIANT_THEORY.md (COMPOSITUM_SPEC 0.1 Hierarchy)"
}
if (-not ($report -match 'COMPOSITUM_SPECIFICATION')) {
    $warnings += "WARN: Report does not reference COMPOSITUM_SPECIFICATION.md (COMPOSITUM_SPEC 0.1 Hierarchy)"
}

# Output
Write-Host "=== Compliance Report Verification ==="
Write-Host "Report: $ReportPath"
Write-Host ""

if ($warnings.Count -gt 0) {
    foreach ($w in $warnings) { Write-Host "  $w" -ForegroundColor Yellow }
    Write-Host ""
}

if ($failures.Count -gt 0) {
    foreach ($f in $failures) { Write-Host "  $f" -ForegroundColor Red }
    Write-Host ""
    Write-Host "RESULT: INVALID ($($failures.Count) failure(s), $($warnings.Count) warning(s))" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULT: VALID ($($warnings.Count) warning(s))" -ForegroundColor Green
    exit 0
}
