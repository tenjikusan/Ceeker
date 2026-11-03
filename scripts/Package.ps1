param([string]$Version = '0.62.0-preview.1', [ValidateSet('Full','Standard')][string]$Edition = 'Full')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Version must contain letters, digits, dots or hyphens.' }
$workspace = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $workspace 'artifacts/releases'
$editionSuffix = if ($Edition -eq 'Standard') { '-Standard' } else { '' }
$packageName = "Ceeker-$Version-win-x64$editionSuffix"
$packageDirectory = Join-Path $releaseRoot $packageName
$archivePath = Join-Path $releaseRoot "$packageName.zip"
if ((Test-Path -LiteralPath $packageDirectory) -or (Test-Path -LiteralPath $archivePath)) { throw 'Release already exists. Use a new version to avoid overwriting it.' }
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
& dotnet publish (Join-Path $workspace 'src/Wormy.App/Wormy.App.csproj') -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -p:PublishTrimmed=false "-p:Version=$Version" -o $packageDirectory
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }
& dotnet publish (Join-Path $workspace 'tools/Ceeker.Actions/Ceeker.Actions.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $packageDirectory
if ($LASTEXITCODE -ne 0) { throw "Actions publish failed: $LASTEXITCODE" }
if ($Edition -eq 'Standard') {
    $models = [IO.Path]::GetFullPath((Join-Path $packageDirectory 'Models'))
    $expected = [IO.Path]::GetFullPath((Join-Path $workspace "artifacts/releases/$packageName/Models"))
    if ($models -ne $expected -or -not $models.StartsWith([IO.Path]::GetFullPath($releaseRoot) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe generated model path.' }
    if (Test-Path -LiteralPath $models) { Remove-Item -LiteralPath $models -Recurse -Force }
}
Copy-Item -LiteralPath (Join-Path $workspace 'docs/INSTALLER-EDITIONS.md') -Destination $packageDirectory
@{ edition=$Edition; version=$Version; aiBundled=($Edition -eq 'Full') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageDirectory 'edition.json')
Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.61.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'docs/WINDOWS-ACTIONS.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $workspace 'docs/QUICKSTART.md') -Destination (Join-Path $packageDirectory 'START-HERE.md')
Copy-Item -LiteralPath (Join-Path $workspace 'docs/FEATURES-0.30.md') -Destination (Join-Path $packageDirectory 'FEATURES-0.30.md')
Copy-Item -LiteralPath (Join-Path $workspace 'docs/FEATURES-0.31.md') -Destination (Join-Path $packageDirectory 'FEATURES-0.31.md')
Copy-Item -LiteralPath (Join-Path $workspace 'docs/FEATURES-0.32.md') -Destination (Join-Path $packageDirectory 'FEATURES-0.32.md')
if ($Version.StartsWith('0.33.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.33.md') -Destination (Join-Path $packageDirectory 'UI-0.33.md') }
if ($Version.StartsWith('0.34.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.34.md') -Destination (Join-Path $packageDirectory 'UI-0.34.md') }
if ($Version.StartsWith('0.35.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.35.md') -Destination (Join-Path $packageDirectory 'UI-0.35.md') }
if ($Version.StartsWith('0.36.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.36.md') -Destination (Join-Path $packageDirectory 'UI-0.36.md') }
if ($Version.StartsWith('0.37.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.37.md') -Destination (Join-Path $packageDirectory 'UI-0.37.md') }
if ($Version.StartsWith('0.39.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.39.md') -Destination (Join-Path $packageDirectory 'UI-0.39.md') }
if ($Version.StartsWith('0.40.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.40.md') -Destination (Join-Path $packageDirectory 'UI-0.40.md') }
if ($Version.StartsWith('0.41.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.41.md') -Destination (Join-Path $packageDirectory 'UI-0.41.md') }
if ($Version.StartsWith('0.43.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.43.md') -Destination (Join-Path $packageDirectory 'UI-0.43.md') }
if ($Version.StartsWith('0.44.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.44.md') -Destination (Join-Path $packageDirectory 'UI-0.44.md') }
if ($Version.StartsWith('0.45.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.45.md') -Destination (Join-Path $packageDirectory 'UI-0.45.md') }
if ($Version.StartsWith('0.46.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.46.md') -Destination (Join-Path $packageDirectory 'UI-0.46.md') }
if ($Version.StartsWith('0.47.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.47.md') -Destination (Join-Path $packageDirectory 'UI-0.47.md') }
if ($Version.StartsWith('0.48.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.48.md') -Destination (Join-Path $packageDirectory 'UI-0.48.md') }
if ($Version.StartsWith('0.49.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.49.md') -Destination (Join-Path $packageDirectory 'UI-0.49.md') }
if ($Version.StartsWith('0.50.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.50.md') -Destination (Join-Path $packageDirectory 'UI-0.50.md') }
if ($Version.StartsWith('0.51.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.51.md') -Destination (Join-Path $packageDirectory 'UI-0.51.md') }
if ($Version.StartsWith('0.52.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.52.md') -Destination (Join-Path $packageDirectory 'UI-0.52.md') }
if ($Version.StartsWith('0.53.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.53.md') -Destination (Join-Path $packageDirectory 'UI-0.53.md') }
if ($Version.StartsWith('0.54.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.54.md') -Destination (Join-Path $packageDirectory 'UI-0.54.md') }
if ($Version.StartsWith('0.55.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.55.md') -Destination (Join-Path $packageDirectory 'UI-0.55.md') }
if ($Version.StartsWith('0.56.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.56.md') -Destination (Join-Path $packageDirectory 'UI-0.56.md') }
if ($Version.StartsWith('0.57.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.57.md') -Destination (Join-Path $packageDirectory 'UI-0.57.md') }
if ($Version.StartsWith('0.58.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.58.md') -Destination (Join-Path $packageDirectory 'UI-0.58.md') }
if ($Version.StartsWith('0.59.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.59.md') -Destination (Join-Path $packageDirectory 'UI-0.59.md') }
if ($Version.StartsWith('0.60.')) { Copy-Item -LiteralPath (Join-Path $workspace 'docs/UI-0.60.md') -Destination (Join-Path $packageDirectory 'UI-0.60.md') }
Copy-Item -LiteralPath (Join-Path $workspace 'docs/THIRD-PARTY.md') -Destination (Join-Path $packageDirectory 'THIRD-PARTY.md')
Copy-Item -LiteralPath (Join-Path $workspace 'build-support/performance.json') -Destination (Join-Path $packageDirectory 'performance.json')
$manifest = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($packageDirectory, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes = $_.Length }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageDirectory 'manifest.json') -Encoding utf8
[IO.Compression.ZipFile]::CreateFromDirectory($packageDirectory, $archivePath, [IO.Compression.CompressionLevel]::Optimal, $true)
$checksum = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
"$checksum  $packageName.zip" | Set-Content -LiteralPath "$archivePath.sha256" -Encoding ascii
Write-Output "Package: $archivePath"
Write-Output "SHA256: $checksum"



















