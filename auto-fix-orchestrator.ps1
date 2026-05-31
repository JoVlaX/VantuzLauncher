#Requires -Version 5.1
<#
.SYNOPSIS
    Автономный оркестратор цикла тест-исправление для VantuzLauncher.

.DESCRIPTION
    Согласно Armatura: Termination guarantee (max 10 iterations),
    Measurability (JSON state), Verifiability (exit codes).
    Полный цикл: build → test → analyze → fix → retry.

.PARAMETER MaxIterations
    Максимальное количество итераций (по умолчанию 10).

.PARAMETER StagnationThreshold
    Количество повторов одной ошибки для остановки (по умолчанию 3).

.PARAMETER AutoFix
    Разрешить автоматическое исправление ошибок.

.EXAMPLE
    .\auto-fix-orchestrator.ps1 -AutoFix

.EXAMPLE
    .\auto-fix-orchestrator.ps1 -MaxIterations 5 -StagnationThreshold 2
#>
[CmdletBinding()]
param(
    [int]$MaxIterations = 10,
    [int]$StagnationThreshold = 3,
    [switch]$AutoFix
)

# ============================================
# CONFIGURATION (Nomadic: относительные пути)
# ============================================
$scriptDir = $PSScriptRoot
$slnPath = Join-Path $scriptDir "VantuzLauncher.sln"
$testScript = Join-Path $scriptDir "test-and-run.ps1"
$stateFile = Join-Path $scriptDir "auto-fix-state.json"
$historyFile = Join-Path $scriptDir "auto-fix-history.log"

$colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
    Iteration = "Magenta"
}

# ============================================
# STATE MANAGEMENT
# ============================================

function Initialize-State {
    $state = @{
        runId = [Guid]::NewGuid().ToString()
        startTime = [DateTime]::UtcNow.ToString("o")
        iterations = 0
        errorsSeen = @()
        lastError = $null
        fixesApplied = 0
        status = "initialized"
        lastCodeHash = $null
    }
    Save-State $state
    return $state
}

function Get-State {
    if (Test-Path $stateFile) {
        return Get-Content $stateFile | ConvertFrom-Json
    }
    return Initialize-State
}

function Save-State {
    param([hashtable]$State)
    $State | ConvertTo-Json -Depth 5 | Out-File $stateFile -Encoding UTF8
}

function Add-History {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File $historyFile -Append -Encoding UTF8
    Write-Host "  $Message" -ForegroundColor $colors.Info
}

# ============================================
# BUILD PHASE
# ============================================

function Invoke-BuildPhase {
    Write-Host "`n[BUILD] Starting..." -ForegroundColor $colors.Iteration
    
    & dotnet build $slnPath -c Release --verbosity minimal 2>&1 | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Gray
    }
    
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -eq 0) {
        Add-History "Build: SUCCESS"
        return @{ Success = $true; Error = $null }
    }
    
    # Capture build errors
    $errors = & dotnet build $slnPath -c Release 2>&1 | Where-Object { $_ -match "error (CS|MSB)\d+:" }
    $errorSummary = if ($errors) { $errors[0] } else { "Build failed" }
    
    Add-History "Build: FAILED - $errorSummary"
    return @{ Success = $false; Error = $errorSummary; Type = "build" }
}

# ============================================
# TEST PHASE
# ============================================

function Invoke-TestPhase {
    Write-Host "`n[TEST] Starting headless test..." -ForegroundColor $colors.Iteration
    
    & $testScript -NoBuild -Timeout 60 2>&1 | ForEach-Object {
        if ($_ -match "FAIL|Error") {
            Write-Host "  $_" -ForegroundColor $colors.Error
        } elseif ($_ -match "PASS|Success") {
            Write-Host "  $_" -ForegroundColor $colors.Success
        } else {
            Write-Host "  $_" -ForegroundColor Gray
        }
    }
    
    $exitCode = $LASTEXITCODE
    $resultPath = Join-Path $scriptDir "test-result.json"
    
    $result = if (Test-Path $resultPath) {
        Get-Content $resultPath | ConvertFrom-Json
    } else { $null }
    
    if ($exitCode -eq 0 -and $result -and $result.status -eq "success") {
        Add-History "Test: SUCCESS (duration: $($result.duration))"
        return @{ Success = $true; Error = $null; Result = $result }
    }
    
    $errorMsg = if ($result) { $result.errorMessage } else { "Test execution failed" }
    Add-History "Test: FAILED - $errorMsg"
    return @{ 
        Success = $false 
        Error = $errorMsg 
        Type = "test"
        Result = $result 
        ExitCode = $exitCode 
    }
}

# ============================================
# ERROR ANALYSIS
# ============================================

function Test-CanAutoFix {
    param([string]$Error, [string]$Type)
    
    # Build errors that can be auto-fixed
    $autoFixPatterns = @(
        "CS1002.*; expected"
        "CS1513.*} expected"
        "CS0246.*type or namespace.*could not be found"
        "CS0103.*does not exist"
        "CS0234.*type or namespace.*does not exist"
        "CS1061.*does not contain"
    )
    
    # Test errors that can be auto-fixed
    $autoFixTestPatterns = @(
        "boot.json not found"
        "FileNotFoundException"
        "DirectoryNotFoundException"
        "NullReferenceException"
    )
    
    $patterns = if ($Type -eq "build") { $autoFixPatterns } else { $autoFixTestPatterns }
    
    foreach ($pattern in $patterns) {
        if ($Error -match $pattern) {
            return @{ CanFix = $true; Pattern = $pattern }
        }
    }
    
    return @{ CanFix = $false }
}

# ============================================
# RETRY GUARD (Termination per INVARIANT_THEORY)
# ============================================

function Test-RetryGuard {
    param([hashtable]$State, [string]$CurrentError)
    
    # Check max iterations
    if ($State.iterations -ge $MaxIterations) {
        return @{ CanContinue = $false; Reason = "Max iterations ($MaxIterations) reached" }
    }
    
    # Check stagnation (same error repeated)
    $errorCount = ($State.errorsSeen | Where-Object { $_ -eq $CurrentError }).Count
    if ($errorCount -ge $StagnationThreshold) {
        return @{ CanContinue = $false; Reason = "Stagnation: '$CurrentError' repeated $errorCount times" }
    }
    
    return @{ CanContinue = $true }
}

# ============================================
# FIX PHASE (Placeholder - would integrate with AI fix system)
# ============================================

function Invoke-FixPhase {
    param([hashtable]$ErrorInfo)
    
    Write-Host "`n[FIX] Analyzing error..." -ForegroundColor $colors.Warning
    Write-Host "  Error: $($ErrorInfo.Error)" -ForegroundColor $colors.Error
    Write-Host "  Type: $($ErrorInfo.Type)" -ForegroundColor Gray
    
    $canFix = Test-CanAutoFix -Error $ErrorInfo.Error -Type $ErrorInfo.Type
    
    if (-not $canFix.CanFix) {
        Write-Host "  Cannot auto-fix this error type" -ForegroundColor $colors.Error
        return @{ Fixed = $false; Reason = "Not auto-fixable" }
    }
    
    Write-Host "  Pattern matched: $($canFix.Pattern)" -ForegroundColor $colors.Info
    Write-Host "  AUTO-FIX MODE: Would apply fix here" -ForegroundColor $colors.Warning
    
    # Note: Actual fix implementation requires code analysis and modification
    # This orchestrator manages the loop; fixes are applied by code analysis tools
    
    return @{ 
        Fixed = $true 
        Pattern = $canFix.Pattern
        Note = "Fix applied externally - retry will verify"
    }
}

# ============================================
# MAIN ORCHESTRATION LOOP
# ============================================

function Start-AutoFixCycle {
    Write-Host "`n╔══════════════════════════════════════════════════════════╗" -ForegroundColor $colors.Iteration
    Write-Host "║     VANTUZ LAUNCHER AUTO-FIX ORCHESTRATOR               ║" -ForegroundColor $colors.Iteration
    Write-Host "║     Termination Guarantee: max $MaxIterations iterations  ║" -ForegroundColor $colors.Iteration
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor $colors.Iteration
    
    $state = Initialize-State
    $autoFixFlag = if ($AutoFix) { "Enabled" } else { "Disabled" }
    Add-History "Orchestrator started (AutoFix: $autoFixFlag)"
    
    while ($true) {
        $state.iterations++
        Write-Host "`n════════════════════════════════════════════════════════════" -ForegroundColor $colors.Iteration
        Write-Host "ITERATION $($state.iterations) of $MaxIterations" -ForegroundColor $colors.Iteration -BackgroundColor Black
        Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor $colors.Iteration
        
        # PHASE 1: BUILD
        $buildResult = Invoke-BuildPhase
        
        if (-not $buildResult.Success) {
            $state.lastError = $buildResult.Error
            $state.errorsSeen += $buildResult.Error
            
            # Check guard
            $guard = Test-RetryGuard $state $buildResult.Error
            if (-not $guard.CanContinue) {
                Add-History "STOPPED: $($guard.Reason)"
                return @{ 
                    Success = $false 
                    Phase = "build" 
                    Reason = $guard.Reason
                    LastError = $buildResult.Error
                    Iterations = $state.iterations
                }
            }
            
            # Attempt fix
            if ($AutoFix) {
                $fixResult = Invoke-FixPhase $buildResult
                if (-not $fixResult.Fixed) {
                    Add-History "Fix failed: $($fixResult.Reason)"
                    return @{
                        Success = $false
                        Phase = "build"
                        Reason = $fixResult.Reason
                        LastError = $buildResult.Error
                        Iterations = $state.iterations
                    }
                }
                $state.fixesApplied++
                Add-History "Fix applied, retrying..."
                continue
            } else {
                Add-History "AutoFix disabled, stopping"
                return @{
                    Success = $false
                    Phase = "build"
                    Reason = "Build failed, AutoFix not enabled"
                    LastError = $buildResult.Error
                    Iterations = $state.iterations
                }
            }
        }
        
        # PHASE 2: TEST
        $testResult = Invoke-TestPhase
        
        if (-not $testResult.Success) {
            $state.lastError = $testResult.Error
            $state.errorsSeen += $testResult.Error
            
            # Check guard
            $guard = Test-RetryGuard $state $testResult.Error
            if (-not $guard.CanContinue) {
                Add-History "STOPPED: $($guard.Reason)"
                return @{
                    Success = $false
                    Phase = "test"
                    Reason = $guard.Reason
                    LastError = $testResult.Error
                    Iterations = $state.iterations
                    TestResult = $testResult.Result
                }
            }
            
            # Attempt fix
            if ($AutoFix) {
                $fixResult = Invoke-FixPhase $testResult
                if (-not $fixResult.Fixed) {
                    Add-History "Fix failed: $($fixResult.Reason)"
                    return @{
                        Success = $false
                        Phase = "test"
                        Reason = $fixResult.Reason
                        LastError = $testResult.Error
                        Iterations = $state.iterations
                        TestResult = $testResult.Result
                    }
                }
                $state.fixesApplied++
                Add-History "Fix applied, retrying..."
                continue
            } else {
                Add-History "AutoFix disabled, stopping"
                return @{
                    Success = $false
                    Phase = "test"
                    Reason = "Test failed, AutoFix not enabled"
                    LastError = $testResult.Error
                    Iterations = $state.iterations
                    TestResult = $testResult.Result
                }
            }
        }
        
        # SUCCESS
        $state.status = "success"
        Save-State $state
        Add-History "SUCCESS: All phases passed"
        
        Write-Host "`n╔══════════════════════════════════════════════════════════╗" -ForegroundColor $colors.Success
        Write-Host "║                 ✅ SUCCESS" -ForegroundColor $colors.Success
        Write-Host "║     Iterations: $($state.iterations)" -ForegroundColor $colors.Success
        Write-Host "║     Fixes applied: $($state.fixesApplied)" -ForegroundColor $colors.Success
        $duration = if ($state.startTime) { 
            ([DateTime]::UtcNow - [DateTime]$state.startTime).ToString()
        } else { 
            "N/A" 
        }
        Write-Host "║     Duration: $duration" -ForegroundColor $colors.Success
        Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor $colors.Success
        
        return @{
            Success = $true
            Iterations = $state.iterations
            FixesApplied = $state.fixesApplied
            TestResult = $testResult.Result
        }
}

# ============================================
# ENTRY POINT
# ============================================

$finalResult = Start-AutoFixCycle

# Generate final report
$reportPath = Join-Path $scriptDir "auto-fix-report.json"
$finalResult | ConvertTo-Json -Depth 5 | Out-File $reportPath -Encoding UTF8

Write-Host "`nReport saved to: $reportPath" -ForegroundColor $colors.Info

if ($finalResult.Success) { exit 0 } else { exit 1 }
