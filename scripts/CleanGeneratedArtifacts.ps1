param([switch]$RemoveLastWormyRelease)
$ErrorActionPreference='Stop'
$workspaceRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $workspaceRoot 'artifacts'))
if(-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'src/Wormy.App/Wormy.App.csproj'))){throw 'Unexpected workspace'}
$targets=@()
$targets += Get-ChildItem -LiteralPath (Join-Path $artifactRoot 'releases') | Where-Object { $_.Name -match '^Wormy-\d+\.\d+\.\d+-preview\.\d+-(Setup-x64\.exe(?:\.sha256)?|win-x64(?:\.zip(?:\.sha256)?)?)$' -and ($RemoveLastWormyRelease -or $_.Name -notlike 'Wormy-0.54.0-preview.1-*') }
$targets += Get-ChildItem -LiteralPath $artifactRoot -Directory | Where-Object { $_.Name -match '^(package-check-|clean-machine-|drive-ui-|final-052-fixture$|final-installed-ui-|final-ui-|finish-ui-|native-final-|native-thumbnails-|ui-048-|visual-check-)' }
$targets += Get-ChildItem -LiteralPath $artifactRoot -File | Where-Object { $_.Extension -in @('.png','.jpg','.jpeg','.xbf','.db') -or $_.Name -match '\.db\.(view\.json|wal|shm)$' }
$targets += Get-ChildItem -LiteralPath $artifactRoot -Directory -Filter 'background-release-*' | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Filter 'ocr' }
$running=Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('Wormy.App.exe','Ceeker.App.exe') }
$removed=@();$bytes=0L
foreach($target in $targets){
 $absolute=[IO.Path]::GetFullPath($target.FullName)
 if(-not $absolute.StartsWith($artifactRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Target escaped artifacts: $absolute"}
 if($running | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($absolute,[StringComparison]::OrdinalIgnoreCase) }){throw "Active test target: $absolute"}
 if(($target.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Linked target rejected: $absolute"}
 $descendants=if($target.PSIsContainer){@(Get-ChildItem -LiteralPath $absolute -Recurse -Force)}else{@($target)}
 if($descendants | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}){throw "Linked descendant rejected: $absolute"}
 $length=($descendants | Where-Object {-not $_.PSIsContainer} | Measure-Object -Property Length -Sum).Sum
 Remove-Item -LiteralPath $absolute -Recurse -Force
 $bytes+=$length;$removed+=[pscustomobject]@{path=$absolute;bytes=$length}
}
$report=[ordered]@{utc=[DateTime]::UtcNow;removedCount=$removed.Count;bytes=$bytes;removed=$removed;retained='Real library/preferences, connected originals, reference ZIP, source/tests, toolchain, logs and latest Ceeker release'}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactRoot ('cleanup-055'+$(if($RemoveLastWormyRelease){'-final'}else{''})+'.json'))
[pscustomobject]@{removedCount=$removed.Count;GB=[Math]::Round($bytes/1e9,2)} | ConvertTo-Json

