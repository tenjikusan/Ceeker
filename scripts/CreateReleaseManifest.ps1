param([string]$Version = '0.26.0-preview.1', [Parameter(Mandatory)][uri]$InstallerUrl, [string]$Notes = 'Wormy preview update')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-preview\.\d+)?$') { throw 'Invalid release version.' }
if ($InstallerUrl.Scheme -ne 'https' -or $InstallerUrl.UserInfo -or $InstallerUrl.IsLoopback -or $InstallerUrl.HostNameType -ne 'Dns') { throw 'Use an HTTPS website installer URL without credentials.' }
if ($Notes.Length -gt 8000) { throw 'Release notes exceed 8000 characters.' }
$workspace = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $workspace "artifacts/releases/Ceeker-$Version-Setup-x64.exe"
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
$expected = ((Get-Content -LiteralPath "$installer.sha256" -Raw).Trim() -split '\s+')[0]
if ($hash -ne $expected) { throw 'Installer checksum mismatch.' }
$output = Join-Path $workspace "artifacts/releases/release-$Version.json"
if (Test-Path -LiteralPath $output) { throw 'Manifest already exists.' }
[ordered]@{ version=$Version; installerUrl=$InstallerUrl.AbsoluteUri; sha256=$hash; notes=$Notes } | ConvertTo-Json | Set-Content -LiteralPath $output -Encoding utf8
Write-Output "Manifest ready: $output. Publish it as latest.json only after its installer is available at the specified URL."
