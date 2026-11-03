param([string]$Version='0.62.0-preview.1', [string]$Compiler)
$ErrorActionPreference='Stop'
if($Version -notmatch '^[a-zA-Z0-9.-]+$'){throw 'Invalid version.'}
$workspace=Split-Path -Parent $PSScriptRoot
$source=Join-Path $workspace 'src/Wormy.App'
$modelRoot=Join-Path $source 'Models'
$manifest=Get-Content -LiteralPath (Join-Path $modelRoot 'model-hashes.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest){
    $path=[IO.Path]::GetFullPath((Join-Path $modelRoot $entry.name))
    if(-not $path.StartsWith([IO.Path]::GetFullPath($modelRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Model path escaped root.'}
    if((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256){throw "Model integrity mismatch: $($entry.name)"}
}
if((Get-ChildItem -LiteralPath $modelRoot -Recurse -File).Count -ne $manifest.Count+1){throw 'Unexpected model files.'}
$output=Join-Path $workspace "artifacts/releases/Ceeker-$Version-AI-Pack-x64.exe"
if(Test-Path -LiteralPath $output){throw 'AI pack exists. Use a new version.'}
if(-not $Compiler){$Compiler=Join-Path $workspace 'artifacts/tools/inno/ISCC.exe'}
& $Compiler "/DPackVersion=$Version" "/DSourceRoot=$source" (Join-Path $workspace 'installer/AiPack.iss')
if($LASTEXITCODE -ne 0){throw 'AI pack compilation failed.'}
$hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($output))" | Set-Content -LiteralPath "$output.sha256" -Encoding ascii
Write-Output "AI pack: $output"
Write-Output "SHA256: $hash"
