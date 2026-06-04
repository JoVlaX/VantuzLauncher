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
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$slnPath = Join-Path $scriptDir "VantuzLauncher.sln"
$testScript = Join-Path $scriptDir "test-and-run.ps1"
$validationScript = Join-Path $scriptDir "validate-build-paths.ps1"
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
# CODE CHANGE VERIFICATION (Measurability per INVARIANT_THEORY.md §1.2)
# ============================================

function Get-CodeHash {
    # Per DEVIATION-004 closure: include .cs and .json for measurable state change
    $csFiles = Get-ChildItem -Path $scriptDir -Recurse -Filter "*.cs" | Sort-Object { $_.FullName.Substring($scriptDir.Length).ToLowerInvariant() }
    $jsonFiles = Get-ChildItem -Path $scriptDir -Recurse -Filter "*.json" | Where-Object { $_.Name -notmatch "node_modules|bin|obj" } | Sort-Object { $_.FullName.Substring($scriptDir.Length).ToLowerInvariant() }
    $files = @($csFiles) + @($jsonFiles)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    foreach ($file in $files) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        if ($bytes.Length -gt 0) { $sha.TransformBlock($bytes, 0, $bytes.Length, $null, 0) | Out-Null }
    }
    $sha.TransformFinalBlock(@(), 0, 0) | Out-Null
    return [BitConverter]::ToString($sha.Hash).Replace("-", "").ToLowerInvariant()
}

# ============================================
# STATE MANAGEMENT
# ============================================

function Initialize-State {
    $state = @{
        runId = [Guid]::NewGuid().ToString()
        startTime = [DateTime]::UtcNow.ToString("o")
        iterations = [int]0
        errorsSeen = @()
        lastError = $null
        fixesApplied = [int]0
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

    if ($exitCode -ne 0) {
        $errors = & dotnet build $slnPath -c Release 2>&1 | Where-Object { $_ -match "error (CS|MSB)\d+:" }
        $errorSummary = if ($errors) { $errors[0] } else { "Build failed" }
        Add-History "Build: FAILED - $errorSummary"
        return @{ Success = $false; Error = $errorSummary; Type = "build" }
    }

    # --- Dual-path validation: dotnet run --project per INVARIANT_THEORY.md §1.2 ---
    if (Test-Path $validationScript) {
        Write-Host "`n[BUILD] Validating dotnet run --project path..." -ForegroundColor $colors.Iteration
        & $validationScript -AssertDotNetRun -AssertPluginsCopied -AssertBootJsonIntegrity 2>&1 | ForEach-Object {
            if ($_ -match "FAIL") { Write-Host "  $_" -ForegroundColor $colors.Error }
            elseif ($_ -match "PASS") { Write-Host "  $_" -ForegroundColor $colors.Success }
            else { Write-Host "  $_" -ForegroundColor Gray }
        }
        if ($LASTEXITCODE -ne 0) {
            $errorSummary = "dotnet run --project validation failed (plugin copy order or integrity). See output above."
            Add-History "Build: FAILED - $errorSummary"
            return @{ Success = $false; Error = $errorSummary; Type = "build" }
        }
    }

    Add-History "Build: SUCCESS"
    return @{ Success = $true; Error = $null }
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
    param([string]$ErrorText, [string]$Type)
    
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
        if ($ErrorText -match $pattern) {
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
# CONCRETE FIX FUNCTIONS (DEVIATION-004 closure)
# ============================================

function Repair-MissingBootJson {
    param([string]$ErrorText)
    $targetBoot = $null
    if ($ErrorText -match "boot\.gui\.json") { $targetBoot = "boot.gui.json" }
    elseif ($ErrorText -match "boot\.headless\.json") { $targetBoot = "boot.headless.json" }
    elseif ($ErrorText -match "boot\.minecraft\.production\.json") { $targetBoot = "boot.minecraft.production.json" }
    elseif ($ErrorText -match "boot\.json") { $targetBoot = "boot.gui.json" }

    if (-not $targetBoot) { return @{ Applied = $false; Reason = "No boot.json target identified" } }

    $template = Join-Path $scriptDir "boot.template.json"
    $target = Join-Path $scriptDir $targetBoot
    if (-not (Test-Path $template)) { return @{ Applied = $false; Reason = "boot.template.json not found" } }
    Copy-Item $template $target -Force
    Write-Host "  [FIX] Copied boot.template.json -> $targetBoot" -ForegroundColor $colors.Success
    return @{ Applied = $true; ChangedFiles = @($target) }
}

function Add-MissingUsingDirective {
    param([string]$ErrorText)
    # Extract missing namespace from CS0234 / CS0246 messages
    if ($ErrorText -notmatch "CS0234|CS0246") { return @{ Applied = $false; Reason = "Not a missing namespace error" } }

    # Try to extract namespace name from error text
    $nsMatch = $ErrorText | Select-String -Pattern "type or namespace name '([^']+)'"
    if (-not $nsMatch) { $nsMatch = $ErrorText | Select-String -Pattern "'([^']+)' could not be found" }
    if (-not $nsMatch) { return @{ Applied = $false; Reason = "Could not extract namespace from error" } }

    $missingNs = $nsMatch.Matches[0].Groups[1].Value
    if ([string]::IsNullOrEmpty($missingNs)) { return @{ Applied = $false; Reason = "Empty namespace extracted" } }

    # Find first .cs file that does not already contain this using
    $csFiles = Get-ChildItem -Path $scriptDir -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" }
    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -notmatch "using\s+$missingNs\s*;") {
            # Insert using directive after any existing using block or at top
            $lines = Get-Content $file.FullName
            $insertIndex = 0
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match "^using\s+" -or $lines[$i] -match "^namespace\s+") { $insertIndex = $i + 1 }
            }
            $newLines = @()
            for ($i = 0; $i -lt $lines.Count; $i++) {
                $newLines += $lines[$i]
                if ($i -eq $insertIndex - 1) { $newLines += "using $missingNs; // auto-fix applied" }
            }
            $newLines | Set-Content $file.FullName -Encoding UTF8
            Write-Host "  [FIX] Added 'using $missingNs;' to $($file.Name)" -ForegroundColor $colors.Success
            return @{ Applied = $true; ChangedFiles = @($file.FullName) }
        }
    }
    return @{ Applied = $false; Reason = "All .cs files already reference $missingNs" }
}

# ============================================
# FIX PHASE (DEVIATION-004: now concrete, not placeholder)
# ============================================

function Invoke-FixPhase {
    param([hashtable]$ErrorInfo, [hashtable]$State)
    
    Write-Host "`n[FIX] Analyzing error..." -ForegroundColor $colors.Warning
    Write-Host "  Error: $($ErrorInfo.Error)" -ForegroundColor $colors.Error
    Write-Host "  Type: $($ErrorInfo.Type)" -ForegroundColor Gray
    
    $canFix = Test-CanAutoFix -Error $ErrorInfo.Error -Type $ErrorInfo.Type
    
    if (-not $canFix.CanFix) {
        Write-Host "  Cannot auto-fix this error type" -ForegroundColor $colors.Error
        return @{ Fixed = $false; Reason = "Not auto-fixable" }
    }
    
    Write-Host "  Pattern matched: $($canFix.Pattern)" -ForegroundColor $colors.Info
    Write-Host "  AUTO-FIX MODE: Applying concrete fix..." -ForegroundColor $colors.Warning
    
    # Per INVARIANT_THEORY.md §1.2: fix must produce measurable code change
    $hashBefore = Get-CodeHash
    $State.lastCodeHash = $hashBefore
    Save-State $State
    
    # DEVIATION-004 RESOLVED: Concrete fix dispatch
    $fixApplied = $false
    $fixReason = $null
    if ($ErrorInfo.Type -eq "test" -and $ErrorInfo.Error -match "boot\.json") {
        $fixResult = Repair-MissingBootJson -ErrorText $ErrorInfo.Error
        $fixApplied = $fixResult.Applied
        $fixReason = $fixResult.Reason
    }
    elseif ($ErrorInfo.Type -eq "build" -and $ErrorInfo.Error -match "CS0234|CS0246") {
        $fixResult = Add-MissingUsingDirective -ErrorText $ErrorInfo.Error
        $fixApplied = $fixResult.Applied
        $fixReason = $fixResult.Reason
    }
    else {
        $fixReason = "No concrete fix implemented for pattern: $($canFix.Pattern)"
    }
    
    if (-not $fixApplied) {
        Write-Host "  [GUARD] Fix not applied: $fixReason" -ForegroundColor $colors.Error
        # Still compute hash in case another process changed files
    }
    
    $hashAfter = Get-CodeHash
    if ($hashAfter -eq $hashBefore) {
        Write-Host "  [GUARD] No code change detected after fix attempt" -ForegroundColor $colors.Error
        return @{ Fixed = $false; Reason = if ($fixReason) { $fixReason } else { "No code change produced" }; Pattern = $canFix.Pattern }
    }
    
    Write-Host "  [OK] Code changed: $hashBefore -> $hashAfter" -ForegroundColor $colors.Success
    
    return @{ 
        Fixed = $true 
        Pattern = $canFix.Pattern
        HashBefore = $hashBefore
        HashAfter = $hashAfter
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
    Add-History -Message "Orchestrator started. AutoFix flag is $autoFixFlag."
    
    while ($true) {
        $state.iterations = [int]$state.iterations + 1
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
                    RunId = $state.runId
                }
            }
            
            # Attempt fix
            if ($AutoFix) {
                $fixResult = Invoke-FixPhase $buildResult $state
                if (-not $fixResult.Fixed) {
                    Add-History "Fix failed: $($fixResult.Reason)"
                    return @{
                        Success = $false
                        Phase = "build"
                        Reason = $fixResult.Reason
                        LastError = $buildResult.Error
                        Iterations = $state.iterations
                        RunId = $state.runId
                    }
                }
                $state.fixesApplied = [int]$state.fixesApplied + 1
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
                    RunId = $state.runId
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
                    RunId = $state.runId
                }
            }
            
            # Attempt fix
            if ($AutoFix) {
                $fixResult = Invoke-FixPhase $testResult $state
                if (-not $fixResult.Fixed) {
                    Add-History "Fix failed: $($fixResult.Reason)"
                    return @{
                        Success = $false
                        Phase = "test"
                        Reason = $fixResult.Reason
                        LastError = $testResult.Error
                        Iterations = $state.iterations
                        TestResult = $testResult.Result
                        RunId = $state.runId
                    }
                }
                $state.fixesApplied = [int]$state.fixesApplied + 1
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
                    RunId = $state.runId
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
            RunId = $state.runId
        }
}

# ============================================
# ENTRY POINT
# ============================================

$cycleResult = Start-AutoFixCycle

# Generate unified report with guaranteed non-null fields per INVARIANT_THEORY.md §1.2
$report = @{
    Success       = [bool]$cycleResult.Success
    Status        = if ($cycleResult.Success) { "success" } else { "failure" }
    Phase         = if ($cycleResult.Phase) { $cycleResult.Phase } else { "none" }
    Reason        = if ($cycleResult.Reason) { $cycleResult.Reason } else { "" }
    LastError     = if ($cycleResult.LastError) { $cycleResult.LastError } else { "" }
    Iterations    = [int]($cycleResult.Iterations)
    FixesApplied  = [int]($cycleResult.FixesApplied)
    TestResult    = if ($cycleResult.TestResult) { $cycleResult.TestResult } else { $null }
    Timestamp     = [DateTime]::UtcNow.ToString("o")
    RunId         = if ($cycleResult.RunId) { $cycleResult.RunId } else { [Guid]::NewGuid().ToString() }
}

$reportPath = Join-Path $scriptDir "auto-fix-report.json"
$report | ConvertTo-Json | Out-File $reportPath -Encoding UTF8

Write-Host "`nReport saved to: $reportPath" -ForegroundColor $colors.Info

if ($report.Success) { exit 0 } else { exit 1 }
