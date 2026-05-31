<#
.SYNOPSIS
    Автономный тестовый пайплайн для VantuzLauncher.

.DESCRIPTION
    Согласно Armatura: SRP (только оркестрация), Nomadic ($PSScriptRoot), Composability (exit codes).
    Полный цикл: build → run → report.

.PARAMETER Username
    Имя пользователя для тестового входа.

.PARAMETER Password
    Пароль для тестового входа.

.PARAMETER Ram
    Количество RAM в МБ (по умолчанию 4096).

.PARAMETER Timeout
    Таймаут выполнения в секундах (по умолчанию 300).

.PARAMETER NoBuild
    Пропустить этап сборки (использовать существующий .exe).

.EXAMPLE
    .\test-and-run.ps1 -Username "test" -Password "test123"

.EXAMPLE
    .\test-and-run.ps1 -NoBuild  # Только запуск, без сборки

.NOTES
    Exit codes:
        0 - Успех
        1 - Ошибка сборки
        2 - Ошибка выполнения
        3 - Таймаут
#>
[CmdletBinding()]
param(
    [string]$Username = "test",
    [string]$Password = "test",
    [int]$Ram = 4096,
    [int]$Timeout = 300,
    [switch]$NoBuild
)

# ============================================
# CONFIGURATION (Nomadic: все пути относительные)
# ============================================
$scriptDir = $PSScriptRoot
$slnPath = Join-Path $scriptDir "VantuzLauncher.sln"
$projectPath = Join-Path $scriptDir "VantuzLauncher.csproj"
$exePath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\VantuzLauncher.exe"
$fallbackExePath = Join-Path $scriptDir "bin\Debug\net8.0-windows\VantuzLauncher.exe"
$reportPath = Join-Path $scriptDir "test-report.log"
$resultPath = Join-Path $scriptDir "test-result.json"

# Цвета для вывода
$colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
}

# ============================================
# FUNCTIONS (SRP: каждая функция делает одно дело)
# ============================================

function Write-Step {
    param([string]$Message)
    Write-Host "`n[$(Get-Date -Format 'HH:mm:ss')] === $Message ===" -ForegroundColor $colors.Info
}

function Write-Result {
    param(
        [string]$Status,
        [string]$Message
    )
    $color = switch ($Status) {
        "PASS" { $colors.Success }
        "FAIL" { $colors.Error }
        "WARN" { $colors.Warning }
        default { $colors.Info }
    }
    Write-Host "[$Status] $Message" -ForegroundColor $color
}

function Invoke-Build {
    param([string]$SolutionPath)

    Write-Step "BUILD"

    if (-not (Test-Path $SolutionPath)) {
        Write-Result "FAIL" "Solution not found: $SolutionPath"
        return $false
    }

    # Очистка перед сборкой
    Write-Host "Cleaning previous builds..."
    & dotnet clean $SolutionPath --verbosity quiet 2>&1 | Out-Null

    # Сборка в Release
    Write-Host "Building solution..."
    $buildOutput = & dotnet build $SolutionPath -c Release --verbosity minimal 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Result "FAIL" "Build failed with exit code $exitCode"
        Write-Host $buildOutput -ForegroundColor $colors.Error
        return $false
    }

    Write-Result "PASS" "Build successful"
    return $true
}

function Find-Executable {
    param(
        [string]$Primary,
        [string]$Fallback
    )

    if (Test-Path $Primary) {
        return $Primary
    }

    if (Test-Path $Fallback) {
        Write-Result "WARN" "Using fallback executable: $Fallback"
        return $Fallback
    }

    # Поиск в bin
    $candidates = Get-ChildItem -Path (Join-Path $scriptDir "bin") -Filter "VantuzLauncher.exe" -Recurse -ErrorAction SilentlyContinue
    if ($candidates) {
        return $candidates[0].FullName
    }

    return $null
}

function Invoke-HeadlessTest {
    param(
        [string]$Executable,
        [string]$Username,
        [string]$Password,
        [int]$Ram,
        [int]$TimeoutSeconds
    )

    Write-Step "RUN"
    Write-Host "Executable: $Executable"
    Write-Host "Username: $Username"
    Write-Host "RAM: $Ram MB"
    Write-Host "Timeout: $TimeoutSeconds sec"

    $arguments = @(
        "--headless"
        "--username=$Username"
        "--password=$Password"
        "--ram=$Ram"
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Executable
    $psi.Arguments = $arguments -join " "
    $psi.WorkingDirectory = $scriptDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    $output = New-Object System.Text.StringBuilder
    $error = New-Object System.Text.StringBuilder

    $outputEvent = { $output.AppendLine($EventArgs.Data) }
    $errorEvent = { $error.AppendLine($EventArgs.Data) }

    Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outputEvent | Out-Null
    Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action $errorEvent | Out-Null

    Write-Host "Starting process..."
    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    # Ожидание с таймаутом
    $completed = $process.WaitForExit($TimeoutSeconds * 1000)

    if (-not $completed) {
        Write-Result "FAIL" "Process timed out after $TimeoutSeconds seconds"
        $process.Kill()
        return @{ Success = $false; ExitCode = 3; Output = $output.ToString(); Error = $error.ToString() }
    }

    $process.WaitForExit()  # Дожидаемся завершения обработки вывода

    $exitCode = $process.ExitCode
    $outputStr = $output.ToString()
    $errorStr = $error.ToString()

    # Вывод консоли
    if ($outputStr) {
        Write-Host "`n--- STDOUT ---" -ForegroundColor $colors.Info
        Write-Host $outputStr
    }
    if ($errorStr) {
        Write-Host "`n--- STDERR ---" -ForegroundColor $colors.Warning
        Write-Host $errorStr -ForegroundColor $colors.Error
    }

    $success = ($exitCode -eq 0)
    return @{
        Success = $success
        ExitCode = $exitCode
        Output = $outputStr
        Error = $errorStr
    }
}

function Get-TestResult {
    param([string]$ResultPath)

    if (-not (Test-Path $ResultPath)) {
        return $null
    }

    try {
        $content = Get-Content $ResultPath -Raw
        return $content | ConvertFrom-Json -Depth 10
    }
    catch {
        Write-Result "WARN" "Failed to parse test-result.json: $_"
        return $null
    }
}

function Write-Report {
    param(
        [hashtable]$BuildResult,
        [hashtable]$RunResult,
        [object]$TestResult,
        [string]$ReportPath
    )

    Write-Step "REPORT"

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $lines = @(
        "VANTUZ LAUNCHER TEST REPORT"
        "Generated: $timestamp"
        "================================"
        ""
        "BUILD: $(if ($BuildResult.Success) { 'PASS' } else { 'FAIL' })"
        "RUN: $(if ($RunResult.Success) { 'PASS' } else { 'FAIL' }) (Exit Code: $($RunResult.ExitCode))"
        ""
    )

    if ($TestResult) {
        $lines += @(
            "TEST DETAILS:"
            "  Status: $($TestResult.status)"
            "  Duration: $($TestResult.duration)"
        )

        if ($TestResult.errorMessage) {
            $lines += "  Error: $($TestResult.errorMessage)"
        }

        if ($TestResult.logs -and $TestResult.logs.Count -gt 0) {
            $lines += @("", "LOGS:")
            foreach ($log in $TestResult.logs) {
                $lines += "  $log"
            }
        }
    }

    $lines += ""
    $report = $lines -join "`n"

    # Консоль
    Write-Host $report

    # Файл
    $report | Out-File $ReportPath -Encoding UTF8
    Write-Result "INFO" "Report saved to: $ReportPath"

    return $report
}

# ============================================
# MAIN EXECUTION
# ============================================

Write-Host "`nVANTUZ LAUNCHER TEST PIPELINE" -ForegroundColor $colors.Info
Write-Host "Working directory: $scriptDir"
Write-Host "Report will be saved to: $reportPath"

# --- BUILD ---
$buildSuccess = $true
if (-not $NoBuild) {
    $buildSuccess = Invoke-Build -SolutionPath $slnPath
}
else {
    Write-Step "BUILD (SKIPPED)"
    Write-Result "WARN" "Using existing executable (--NoBuild)"
}

if (-not $buildSuccess) {
    Write-Result "FAIL" "Build phase failed. Cannot continue."
    exit 1
}

# --- FIND EXECUTABLE ---
$exe = Find-Executable -Primary $exePath -Fallback $fallbackExePath
if (-not $exe) {
    Write-Result "FAIL" "VantuzLauncher.exe not found. Build may have failed."
    exit 1
}

# --- RUN ---
$runResult = Invoke-HeadlessTest -Executable $exe -Username $Username -Password $Password -Ram $Ram -TimeoutSeconds $Timeout

# --- READ JSON RESULT ---
Start-Sleep -Milliseconds 500  # Даём время на запись файла
$testResult = Get-TestResult -ResultPath $resultPath

# --- GENERATE REPORT ---
$null = Write-Report -BuildResult @{ Success = $buildSuccess } -RunResult $runResult -TestResult $testResult -ReportPath $reportPath

# --- FINAL STATUS ---
Write-Step "FINAL STATUS"
if ($runResult.Success) {
    Write-Result "PASS" "All tests passed successfully"
    exit 0
}
else {
    Write-Result "FAIL" "Tests failed with exit code $($runResult.ExitCode)"
    exit $runResult.ExitCode
}
