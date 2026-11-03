param([string]$Version = '0.62.0-preview.1', [string]$Compiler, [switch]$Compact, [ValidateSet('Full','Standard')][string]$Edition = 'Full')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Invalid version.' }
$workspace = Split-Path -Parent $PSScriptRoot
if (-not $Compiler) { $Compiler = Join-Path $workspace 'artifacts/tools/inno/ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6.7+ and pass -Compiler with its ISCC.exe path.' }
& (Join-Path $PSScriptRoot 'VerifyPackage.ps1') -Version $Version -Edition $Edition
$payload = Join-Path $workspace "artifacts/releases/Ceeker-$Version-win-x64"
$editionSuffix = if ($Edition -eq 'Standard') { '-Standard' } else { '' }
$suffix = $editionSuffix + $(if ($Compact) { "-Compact" } else { "" })
$output = Join-Path $workspace "artifacts/releases/Ceeker-$Version-Setup-x64$suffix.exe"
if (Test-Path -LiteralPath $output) { throw 'Installer exists; use a new version.' }
# Compile the verified extracted payload, not a mutable publish directory.
$report = Get-Content -LiteralPath (Join-Path $workspace 'artifacts/package-verification.json') -Raw | ConvertFrom-Json
$payload = $report.extractedDirectory
& $Compiler "/DAppVersion=$Version" "/DPayloadDir=$payload" "/DOutputSuffix=$suffix" (Join-Path $workspace 'installer/Wormy.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($output))" | Set-Content -LiteralPath "$output.sha256" -Encoding ascii
Write-Output "Installer: $output"
Write-Output "SHA256: $hash"








