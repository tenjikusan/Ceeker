param([string]$Version = '0.60.0-preview.2')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Invalid version.' }
$workspace = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $workspace "artifacts/releases/Ceeker-$Version-Setup-x64.exe"
$expected = ((Get-Content -LiteralPath "$installer.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $installer).Hash -ne $expected) { throw 'Installer checksum mismatch.' }
$bundle = Join-Path $workspace "artifacts/clean-machine-$Version"
if (Test-Path -LiteralPath $bundle) { throw 'Bundle already exists.' }
$inputDirectory = Join-Path $bundle 'input'
$releaseDirectory = Join-Path $inputDirectory 'artifacts/releases'
$scriptDirectory = Join-Path $inputDirectory 'scripts'
$resultsDirectory = Join-Path $bundle 'results'
New-Item -ItemType Directory -Path $releaseDirectory,$scriptDirectory,$resultsDirectory -Force | Out-Null
Copy-Item -LiteralPath $installer,"$installer.sha256" -Destination $releaseDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VerifyInstaller.ps1') -Destination $scriptDirectory
$bootstrap = @'
$ErrorActionPreference = 'Stop'
$work = 'C:\WormyValidation'
try {
    Copy-Item -LiteralPath 'C:\WormyInput' -Destination $work -Recurse
    & "$work\scripts\VerifyInstaller.ps1" -Version '__VERSION__'
    Copy-Item -LiteralPath "$work\artifacts\installer-verification.json" -Destination 'C:\WormyResults'
} catch {
    $_ | Out-String | Set-Content 'C:\WormyResults\failure.txt'
} finally {
    Get-ChildItem "$work\artifacts" -Directory -Filter 'installer-check-*' -ErrorAction SilentlyContinue | ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Filter '*.log' -File | Copy-Item -Destination 'C:\WormyResults'
    }
}
'@
$bootstrap.Replace('__VERSION__',$Version) | Set-Content -LiteralPath (Join-Path $inputDirectory 'Run.ps1')
$inputXml = [Security.SecurityElement]::Escape($inputDirectory)
$resultsXml = [Security.SecurityElement]::Escape($resultsDirectory)
@"
<Configuration>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$inputXml</HostFolder><SandboxFolder>C:\WormyInput</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$resultsXml</HostFolder><SandboxFolder>C:\WormyResults</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\WormyInput\Run.ps1</Command></LogonCommand>
</Configuration>
"@ | Set-Content -LiteralPath (Join-Path $bundle 'Wormy.wsb')
Write-Output "Prepared $bundle. No VM was launched; open Wormy.wsb on a Windows Sandbox-enabled host."





