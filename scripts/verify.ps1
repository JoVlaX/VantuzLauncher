# Armatura Verification Protocol
# INVARIANT_THEORY.md compliant - explicit, measurable, no hidden mechanisms
[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Build", "Analyzers", "Smoke", "All")]
    [string]$Phase = "All"
)

$ErrorActionPreference = "Stop"

function Write-Header {
    param([string]$text)
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-Phase {
    param([string]$text)
    Write-Host "[PHASE] $text" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$text)
    Write-Host "✓ $text" -ForegroundColor Green
}

function Write-Error {
    param([string]$text)
    Write-Host "✗ $text" -ForegroundColor Red
}

function Write-Warning {
    param([string]$text)
    Write-Host "⚠ $text" -ForegroundColor Yellow
}

Write-Header "Armatura Verification Protocol"

switch ($Phase) {
    "Build" { 
        Write-Phase "Build Verification"
        dotnet build c:\000\projects\compositum\VantuzLauncher.sln --configuration Debug --verbosity quiet
        if ($LASTEXITCODE -ne 0) { 
            Write-Error "Build failed"
            exit 1 
        }
        Write-Success "Build successful"
    }
    
    "Analyzers" {
        Write-Phase "Architectural Analyzers (ARM007-ARM013)"
        $output = dotnet build c:\000\projects\compositum\VantuzLauncher.sln --configuration Debug --verbosity normal 2>&1
        
        # ARM011 - Component Scope (Error level)
        $arm011 = $output | Select-String "ARM011"
        if ($arm011) { 
            Write-Error "ARM011: Component Scope violations detected"
            $arm011 | ForEach-Object { Write-Error "  $_" }
            exit 1
        }
        
        # ARM012/ARM013 - Context Keys (Warning level, non-blocking)
        $arm012 = $output | Select-String "ARM012"
        $arm013 = $output | Select-String "ARM013"
        
        if ($arm012) {
            Write-Warning "ARM012: Unmatched context keys detected"
            $arm012 | ForEach-Object { Write-Warning "  $_" }
        }
        
        if ($arm013) {
            Write-Warning "ARM013: Similar context keys detected"
            $arm013 | ForEach-Object { Write-Warning "  $_" }
        }
        
        Write-Success "Analyzers passed"
    }
    
    "Smoke" {
        Write-Phase "Smoke Test"
        & "$PSScriptRoot\smoke-test.ps1"
    }
    
    "All" {
        & $PSCommandPath -Phase Build
        & $PSCommandPath -Phase Analyzers
        # Smoke test optional for regular verification
        # & $PSCommandPath -Phase Smoke
    }
}

Write-Header "Verification Complete ✓"
