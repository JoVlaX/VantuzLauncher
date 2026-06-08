#Requires -Version 5.1
<#
.SYNOPSIS
    Checks C# source files for F_doc/E_doc presence in XML documentation.

.DESCRIPTION
    Scans all .cs files for public APIs (classes, interfaces, methods, properties)
    and verifies that each has either F_doc/E_doc comments or a [HYPOTHESIS] marker.

.PARAMETER SourcePath
    Root directory of C# source files.

.PARAMETER Threshold
    Minimum percentage of covered APIs to pass (default: 90).

.EXAMPLE
    .\scripts\check-falsifiability.ps1 -SourcePath "."
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [int]$Threshold = 90
)

$ErrorActionPreference = "Stop"

$total = 0
$covered = 0
$missing = @()

$files = Get-ChildItem -Path $SourcePath -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|tests?|Test)\\"
}

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $lines = Get-Content -Path $file.FullName

    # Match public types and members
    $publicApis = [regex]::Matches($content, '(?m)^\s*public\s+(?:abstract\s+|sealed\s+|partial\s+|virtual\s+|override\s+|static\s+)*\w+(?:<[^>]+>)?(?:\[\])?\s+(\w+)(?:\s*\(|\s*\{|$)')

    foreach ($match in $publicApis) {
        $apiName = $match.Groups[1].Value
        $lineNum = $content.Substring(0, $match.Index).Split("`n").Count

        # Skip getters/setters and auto-property backing
        if ($apiName -match "^(get_|set_|add_|remove_)" -or $apiName -eq "") { continue }

        $total++

        # Check preceding 20 lines for F_doc, E_doc, or [HYPOTHESIS]
        $startLine = [Math]::Max(0, $lineNum - 20)
        $context = ($lines[$startLine..($lineNum - 1)] -join "`n")

        $hasFDoc = $context -match "F_doc\s*:"
        $hasEDoc = $context -match "E_doc\s*:"
        $hasHypothesis = $context -match "\[HYPOTHESIS\]"

        if ($hasFDoc -or $hasEDoc -or $hasHypothesis) {
            $covered++
        } else {
            $missing += @{
                File = $file.FullName
                Line = $lineNum
                Api = $apiName
            }
        }
    }
}

$percentage = if ($total -gt 0) { [Math]::Round(($covered / $total) * 100, 2) } else { 0 }

Write-Host "=== Falsifiability Check ==="
Write-Host "Files scanned: $($files.Count)"
Write-Host "Public APIs found: $total"
Write-Host "Falsifiable (F_doc/E_doc/[HYPOTHESIS]): $covered"
Write-Host "Missing falsifiability: $($total - $covered)"
Write-Host "Coverage: $percentage% (threshold: $threshold%)"

if ($missing.Count -gt 0 -and $missing.Count -le 10) {
    Write-Host "`nMissing APIs:"
    foreach ($m in $missing) {
        Write-Host "  $($m.File):$($m.Line) — $($m.Api)"
    }
} elseif ($missing.Count -gt 10) {
    Write-Host "`nFirst 10 missing APIs:"
    foreach ($m in $missing | Select-Object -First 10) {
        Write-Host "  $($m.File):$($m.Line) — $($m.Api)"
    }
}

if ($percentage -ge $Threshold) {
    Write-Host "`nRESULT: PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nRESULT: FAIL" -ForegroundColor Red
    exit 1
}
