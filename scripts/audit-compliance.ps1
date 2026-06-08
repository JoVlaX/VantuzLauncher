#Requires -Version 5.1
<#
.SYNOPSIS
    Automated Armatura compliance audit for C# public APIs.

.DESCRIPTION
    Inventories all public types and members, checks for F_doc/E_doc presence,
    detects CQRS violations (mixed read/write in same interface/class),
    and outputs a machine-readable violation list.

.PARAMETER SourcePath
    Root directory of C# source files.

.PARAMETER OutputPath
    Path to write the JSON violation report.

.EXAMPLE
    .\scripts\audit-compliance.ps1 -SourcePath "." -OutputPath "audit-report.json"
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [Parameter(Mandatory=$true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$violations = @()
$publicApis = @()

# Recursively find all .cs files
$files = Get-ChildItem -Path $SourcePath -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|tests?|Test)\\"
}

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $lines = Get-Content -Path $file.FullName

    # Find public types (class, interface, struct, record, enum)
    $publicTypes = [regex]::Matches($content, '(?m)^\s*public\s+(?:abstract\s+)?(?:sealed\s+)?(?:partial\s+)?(class|interface|struct|record|enum)\s+(\w+)')
    foreach ($match in $publicTypes) {
        $typeName = $match.Groups[2].Value
        $typeKind = $match.Groups[1].Value
        $lineNum = $content.Substring(0, $match.Index).Split("`n").Count

        $api = @{
            File = $file.FullName
            Line = $lineNum
            Kind = $typeKind
            Name = $typeName
            HasFDoc = $false
            HasEDoc = $false
            HasXmlDoc = $false
        }

        # Check preceding 30 lines for XML doc and F_doc/E_doc
        $startLine = [Math]::Max(0, $lineNum - 30)
        $context = ($lines[$startLine..($lineNum - 1)] -join "`n")
        $api.HasXmlDoc = $context -match "///\s*<summary>"
        $api.HasFDoc = $context -match "F_doc\s*:"
        $api.HasEDoc = $context -match "E_doc\s*:"

        $publicApis += $api

        # Violation: public API without F_doc or E_doc
        if (-not $api.HasFDoc -and -not $api.HasEDoc) {
            $violations += @{
                Id = "AUTO-{0:D3}" -f ($violations.Count + 1)
                Severity = "MINOR"
                Rule = "INVARIANT_THEORY §4.1a Document Falsifiability"
                File = $file.FullName
                Line = $lineNum
                Api = "$typeKind $typeName"
                Finding = "No F_doc/E_doc comments found for public API"
                Status = "Open"
            }
        }
    }

    # Find public methods/properties in interfaces/classes
    $publicMembers = [regex]::Matches($content, '(?m)^\s*public\s+(?:abstract\s+)?(?:virtual\s+)?(?:override\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?(?:\[\])?\s+)?(\w+)\s*\(')
    foreach ($match in $publicMembers) {
        $memberName = $match.Groups[1].Value
        $lineNum = $content.Substring(0, $match.Index).Split("`n").Count

        # Skip constructors and special methods
        if ($memberName -eq $null -or $memberName -match "^(get_|set_|add_|remove_|op_)" ) { continue }

        # Check preceding 15 lines for F_doc/E_doc
        $startLine = [Math]::Max(0, $lineNum - 15)
        $context = ($lines[$startLine..($lineNum - 1)] -join "`n")
        $hasFDoc = $context -match "F_doc\s*:"
        $hasEDoc = $context -match "E_doc\s*:"

        if (-not $hasFDoc -and -not $hasEDoc) {
            $violations += @{
                Id = "AUTO-{0:D3}" -f ($violations.Count + 1)
                Severity = "MINOR"
                Rule = "INVARIANT_THEORY §4.1a Document Falsifiability"
                File = $file.FullName
                Line = $lineNum
                Api = "Method $memberName"
                Finding = "No F_doc/E_doc comments found for public member"
                Status = "Open"
            }
        }
    }

    # CQRS Check: interfaces with both async Task (likely command) and non-async or property-like (query)
    $interfaces = [regex]::Matches($content, '(?s)public\s+interface\s+(\w+)\s*\{(.*?)\}')
    foreach ($iface in $interfaces) {
        $ifaceName = $iface.Groups[1].Value
        $ifaceBody = $iface.Groups[2].Value
        $hasAsyncMutator = $ifaceBody -match "Task\s+\w+\s*\([^)]*\)"  # async methods
        $hasQuery = $ifaceBody -match "\w+\s+\w+\s*\{\s*get\s*;" -or # properties
                     $ifaceBody -match "\w+\s+\w+\s*\([^)]*\)\s*;"      # methods returning values

        if ($hasAsyncMutator -and $hasQuery) {
            $lineNum = $content.Substring(0, $iface.Index).Split("`n").Count
            $violations += @{
                Id = "AUTO-{0:D3}" -f ($violations.Count + 1)
                Severity = "MAJOR"
                Rule = "INVARIANT_THEORY §2.2 CQRS Separation"
                File = $file.FullName
                Line = $lineNum
                Api = "Interface $ifaceName"
                Finding = "Interface contains both Command-like (async Task) and Query-like members"
                Status = "Open"
            }
        }
    }
}

# Write JSON report
$report = @{
    GeneratedAt = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssK")
    Tool = "scripts/audit-compliance.ps1"
    TotalFilesScanned = $files.Count
    TotalPublicApis = $publicApis.Count
    TotalViolations = $violations.Count
    Violations = $violations
    PublicApiInventory = $publicApis
}

$report | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath
Write-Host "Audit complete. Scanned $($files.Count) files, found $($publicApis.Count) public APIs, $($violations.Count) violations."
Write-Host "Report written to: $OutputPath"
