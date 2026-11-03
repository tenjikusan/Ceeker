#requires -Version 7
param([string]$Version = '0.32.0-preview.1')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$runRoot = Join-Path (Get-Location) ('artifacts/background-release-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$statusPath = Join-Path (Get-Location) 'artifacts/background-release-status.json'
$report = @{ state='running'; version=$Version; root=$runRoot; startedUtc=[DateTime]::UtcNow; checks=@() }
function Save-Report { $report | ConvertTo-Json -Depth 5 | Set-Content $statusPath }
Save-Report
try {
    & dotnet run --project tests/Wormy.Core.Checks *> (Join-Path $runRoot 'core-full.log')
    if ($LASTEXITCODE -ne 0) { throw 'Core regression checks failed.' }
    $report.checks += 'core-full'; Save-Report
    & dotnet run --project tests/Wormy.Windows.Checks -- --ocr-only (Join-Path $runRoot 'ocr') *> (Join-Path $runRoot 'camera-ocr.log')
    if ($LASTEXITCODE -ne 0) { throw 'Camera OCR checks failed.' }
    $report.checks += 'camera-ocr'; Save-Report
    & ./scripts/Package.ps1 -Version $report.version *> (Join-Path $runRoot 'package.log')
    $report.checks += 'published-package'; Save-Report
    & ./scripts/VerifyPackage.ps1 -Version $report.version *> (Join-Path $runRoot 'package-verification.log')
    $report.checks += 'payload-hashes'; Save-Report
    & ./scripts/BuildInstaller.ps1 -Version $report.version *> (Join-Path $runRoot 'installer.log')
    $report.checks += 'installer-built'; $report.state='passed'
} catch { $report.state='failed'; $report.error=$_.Exception.Message }
$report.finishedUtc=[DateTime]::UtcNow
Save-Report
