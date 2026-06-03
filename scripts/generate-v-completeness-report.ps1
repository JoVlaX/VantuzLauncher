# Generate V Completeness Report per COMPOSITUM_SPECIFICATION.md §7.1a
# Outputs V_completeness_report.json to the build output directory

param(
    [string]$OutputDir = "$PSScriptRoot\..\bin\Debug\net8.0-windows"
)

$projectRoot = Resolve-Path "$PSScriptRoot\.."
$builderDir = Join-Path $projectRoot "Vantuz.Builder"
$deviationsDir = Join-Path $projectRoot "docs\deviations"

function Test-Verifier {
    param([string]$FilePath, [string]$Pattern)
    return (Select-String -Path $FilePath -Pattern $Pattern -Quiet)
}

function Test-Deviation {
    param([string]$DeviationFile)
    return (Test-Path $DeviationFile)
}

$verifiers = @(
    @{ Name = "NameVerifier"; File = "$builderDir\PluginNameVerifier.cs"; Pattern = "DiscoverPluginNames"; ArmCode = "ARM-BUILD-020"; Deviation = $null },
    @{ Name = "CQRSVerifier"; File = "$builderDir\PluginNameVerifier.cs"; Pattern = "VerifyCQRS"; ArmCode = "ARM-BUILD-022"; Deviation = $null },
    @{ Name = "ResourceVerifier"; File = "$builderDir\PluginNameVerifier.cs"; Pattern = "ForbiddenResourceTypes"; ArmCode = "ARM-BUILD-023"; Deviation = $null },
    @{ Name = "ScopeVerifier"; File = "$builderDir\PluginNameVerifier.cs"; Pattern = "VerifyScope"; ArmCode = "ARM-BUILD-024"; Deviation = $null },
    @{ Name = "DAGVerifier"; File = "$builderDir\PipelineVisualizer.cs"; Pattern = "DetectCycle"; ArmCode = "ARM-BUILD-021"; Deviation = "$deviationsDir\DEVIATION-006.md" },
    @{ Name = "NomadicVerifier"; File = "$builderDir\PluginNameVerifier.cs"; Pattern = "TransdomainPrimitive"; ArmCode = "ARM-BUILD-026"; Deviation = "$deviationsDir\DEVIATION-007.md" }
)

$report = @{
    timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssK")
    verifiers = @{}
    missing_without_deviation = @()
    build_status = "OK"
}

foreach ($v in $verifiers) {
    $implemented = Test-Verifier -FilePath $v.File -Pattern $v.Pattern
    $hasDeviation = if ($v.Deviation) { Test-Deviation -DeviationFile $v.Deviation } else { $false }

    $status = if ($implemented) { "IMPLEMENTED" } elseif ($hasDeviation) { "DEVIATION" } else { "MISSING" }

    $report.verifiers[$v.Name] = @{
        status = $status
        armCode = $v.ArmCode
        implemented = $implemented
        deviation = $hasDeviation
        sourceFile = Split-Path $v.File -Leaf
    }

    if ($status -eq "MISSING") {
        $report.missing_without_deviation += $v.Name
        $report.build_status = "ERROR"
    }
}

$json = $report | ConvertTo-Json -Depth 5

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$outPath = Join-Path $OutputDir "V_completeness_report.json"
$json | Set-Content -Path $outPath -Encoding UTF8

Write-Host "[V-Completeness] Report generated: $outPath"
Write-Host "[V-Completeness] Status: $($report.build_status)"

if ($report.build_status -eq "ERROR") {
    Write-Error "[ARM-BUILD-027] V Completeness: Missing verifiers without deviation: $($report.missing_without_deviation -join ', ')"
    exit 1
}

exit 0
