$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$runRoot = Join-Path (Get-Location) ('artifacts/background-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$statusPath = Join-Path (Get-Location) 'artifacts/background-validation-status.json'
$report = @{ state='running'; root=$runRoot; startedUtc=[DateTime]::UtcNow; checks=@() }
$report | ConvertTo-Json -Depth 5 | Set-Content $statusPath
function Check([string]$name, [string[]]$arguments) {
    & dotnet @arguments *> (Join-Path $runRoot ($name + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name failed; see its log." }
    $report.checks += $name
    $report | ConvertTo-Json -Depth 5 | Set-Content $statusPath
}
try {
    Check 'core-full' @('run','--project','tests/Wormy.Core.Checks','--no-build')
    Check 'photos-heic' @('run','--project','tests/Wormy.Windows.Checks','--no-build','--','--similarity-check',(Join-Path $runRoot 'photos'))
    Check 'image-ocr' @('run','--project','tests/Wormy.Windows.Checks','--no-build','--','--ocr-only',(Join-Path $runRoot 'ocr'))
    Check 'apps' @('run','--project','tests/Wormy.Windows.Checks','--no-build','--','--apps-check')
    $report.state='passed'
} catch { $report.state='failed'; $report.error=$_.Exception.Message }
$report.finishedUtc=[DateTime]::UtcNow
$report | ConvertTo-Json -Depth 5 | Set-Content $statusPath
