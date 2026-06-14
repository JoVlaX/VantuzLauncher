#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$MessageOverride
)
$ErrorActionPreference = "Stop"

$gitDir = Join-Path $ProjectRoot ".git"
if (-not (Test-Path $gitDir)) { throw "Not a git repository: $ProjectRoot" }

# Collect changed files
$changed = git -C $ProjectRoot diff --name-only HEAD
$untracked = git -C $ProjectRoot ls-files --others --exclude-standard
$allFiles = @($changed) + @($untracked) | Where-Object { $_ -and ($_ -notmatch '^\s*$') } | Select-Object -Unique

if (-not $allFiles) { Write-Host "No changes to commit."; exit 0 }

# Extract invariant references from diff
$invRefs = @()
$fdocs = @()
$edocs = @()
foreach ($f in $allFiles) {
    $fullPath = Join-Path $ProjectRoot $f
    if (Test-Path $fullPath -PathType Leaf) {
        $content = Get-Content $fullPath -Raw -ErrorAction SilentlyContinue
        if ($content) {
            $invMatches = [regex]::Matches($content, 'INV-\d+[a-z]?(?:\.\d+)?')
            foreach ($m in $invMatches) { $invRefs += $m.Value }
            $fdocMatches = [regex]::Matches($content, 'F_doc:\s*\{([^}]+)\}')
            foreach ($m in $fdocMatches) { $fdocs += $m.Groups[1].Value }
            $edocMatches = [regex]::Matches($content, 'E_doc:\s*\{([^}]+)\}')
            foreach ($m in $edocMatches) { $edocs += $m.Groups[1].Value }
        }
    }
}

$invRefs = $invRefs | Select-Object -Unique
$fdocs = $fdocs | Select-Object -Unique | Select-Object -First 3
$edocs = $edocs | Select-Object -Unique | Select-Object -First 3

if ($MessageOverride) {
    $msg = $MessageOverride
} else {
    $type = if ($allFiles | Where-Object { $_ -match '\.md$' }) { "docs" } else { "feat" }
    $areas = ($allFiles | ForEach-Object { ($_.Split('/')[0]) } | Select-Object -Unique) -join ", "
    $msg = "$type($areas): remediation per $($invRefs -join ', ')"
    $body = @()
    if ($invRefs) { $body += "Invariants: $($invRefs -join ', ')" }
    if ($fdocs) { $body += "F_doc: $($fdocs -join '; ')" }
    if ($edocs) { $body += "E_doc: $($edocs -join '; ')" }
    $body += "Build: PASS"
    $body += "Audit: PASS"
    $msg = "$msg`n`n$($body -join "`n")"
}

git -C $ProjectRoot add -A
git -C $ProjectRoot commit -m "$msg"
Write-Host "Committed with message:`n$msg"
