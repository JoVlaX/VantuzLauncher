#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [int]$NonAsciiThreshold = 0
)
$ErrorActionPreference = "Stop"

$utf8Bom = [System.Text.Encoding]::UTF8.GetPreamble()
$failures = @()
$totalChecked = 0

function Test-FileEncoding($filePath) {
    $bytes = [System.IO.File]::ReadAllBytes($filePath) | Select-Object -First 3
    if ($bytes.Count -lt 3) { return $true } # Short files are OK
    $hasBom = ($bytes[0] -eq $utf8Bom[0] -and $bytes[1] -eq $utf8Bom[1] -and $bytes[2] -eq $utf8Bom[2])
    return $hasBom
}

function Test-HasCyrillic($filePath) {
    $content = [System.IO.File]::ReadAllBytes($filePath)
    foreach ($b in $content) {
        if ($b -ge 0x80) { return $true }
    }
    return $false
}

$csFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

foreach ($file in $csFiles) {
    $totalChecked++
    if (Test-HasCyrillic $file.FullName) {
        $hasBom = Test-FileEncoding $file.FullName
        if (-not $hasBom) {
            $failures += $file.FullName
        }
    }
}

Write-Host ""
Write-Host "=== ENCODING VERIFICATION ===" -ForegroundColor Cyan
Write-Host "Files checked: $totalChecked" -ForegroundColor White
Write-Host "Files with non-ASCII content missing BOM: $($failures.Count)" -ForegroundColor $(if ($failures.Count -eq 0) { "Green" } else { "Red" })

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILURES:" -ForegroundColor Red
    foreach ($f in $failures) {
        Write-Host "  [FAIL] $f" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASS: All .cs files with non-ASCII content have UTF-8 BOM." -ForegroundColor Green
exit 0
