#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot
)
$ErrorActionPreference = "Stop"

$gitDir = Join-Path $ProjectRoot ".git"
if (-not (Test-Path $gitDir)) { throw "Not a git repository: $ProjectRoot" }

$issues = @()

# 1. Working tree must be clean
$status = git -C $ProjectRoot status --short
if ($status) { $issues += "Working tree dirty: $($status -join '; ')" }

# 2. Last commit must reference invariants
$lastMsg = git -C $ProjectRoot log -1 --format=%B
if ($lastMsg -notmatch 'INV-\d+[a-z]?(?:\.\d+)?') { $issues += "No INV-XXX reference in last commit message" }

# 3. Last commit must contain F_doc/E_doc
if ($lastMsg -notmatch 'F_doc' -and $lastMsg -notmatch 'E_doc') { $issues += "No F_doc/E_doc in last commit message" }

if ($issues.Count -gt 0) {
    Write-Host "FAIL:" -ForegroundColor Red
    foreach ($i in $issues) { Write-Host "  - $i" -ForegroundColor Red }
    exit 1
} else {
    Write-Host "PASS: Commit protocol satisfied." -ForegroundColor Green
    exit 0
}
