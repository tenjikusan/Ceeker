param([string]$Version = '0.60.0-preview.2', [string]$UpgradeFrom, [switch]$Compact)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Invalid version.' }
$workspace = Split-Path -Parent $PSScriptRoot
$suffix = if ($Compact) { "-Compact" } else { "" }
$installer = Join-Path $workspace "artifacts/releases/Ceeker-$Version-Setup-x64$suffix.exe"
$expected = ((Get-Content -LiteralPath "$installer.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expected) { throw 'Installer checksum mismatch.' }
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{4B6F1C7D-85B5-493A-B57E-96C4C6D40ED1}_is1'
if (Test-Path -LiteralPath $registration) { throw 'Ceeker/Wormy is already installed; refusing to modify that installation.' }
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Ceeker.lnk'
if (Test-Path -LiteralPath $shortcut) { throw 'A Ceeker Start menu shortcut already exists; refusing to overwrite it.' }
$testRoot = Join-Path $workspace ('artifacts/installer-check-' + [guid]::NewGuid().ToString('N'))
$installDirectory = Join-Path $testRoot 'installed'
$libraryDirectory = Join-Path $testRoot 'retained-library'
New-Item -ItemType Directory -Path $libraryDirectory -Force | Out-Null
$sentinel = Join-Path $libraryDirectory 'original.txt'
Set-Content -LiteralPath $sentinel -Value 'Disposable installer preservation check'
$sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
$preferences = Join-Path $libraryDirectory 'library.db.view.json'
Set-Content -LiteralPath $preferences -Value '{"GettingStartedCompleted":true}'
function Run-Setup([string]$exe, [string[]]$arguments) {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup returned $($process.ExitCode). See logs under $testRoot" }
}
$arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/TASKS=',('/DIR="' + $installDirectory + '"'),('/LOG="' + (Join-Path $testRoot 'install.log') + '"'))
if ($UpgradeFrom) {
    if ($UpgradeFrom -notmatch '^[a-zA-Z0-9.-]+$' -or $UpgradeFrom -eq $Version) { throw 'Use a different valid baseline version.' }
    $baseline = Join-Path $workspace "artifacts/releases/Wormy-$UpgradeFrom-Setup-x64.exe"
    $baselineHash = ((Get-Content -LiteralPath "$baseline.sha256" -Raw).Trim() -split '\s+')[0]
    if ((Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash -ne $baselineHash) { throw 'Baseline checksum mismatch.' }
    Run-Setup $baseline $arguments
    if ((Get-ItemProperty -LiteralPath $registration).DisplayVersion -ne $UpgradeFrom) { throw 'Baseline installation version mismatch.' }
    $baselineApp = Start-Process -FilePath (Join-Path $installDirectory 'Wormy.App.exe') -ArgumentList '--library',('"' + (Join-Path $libraryDirectory 'library.db') + '"') -WindowStyle Hidden -PassThru
    try {
        $baselineReady = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Milliseconds 500; $baselineApp.Refresh()
            if ($baselineApp.HasExited) { throw 'Baseline app exited before upgrade.' }
            if ($baselineApp.MainWindowHandle -ne [IntPtr]::Zero -and $baselineApp.Responding) { $baselineReady = $true; break }
        }
        if (-not $baselineReady) { throw 'Baseline startup failed.' }
    } finally { $baselineApp.Refresh(); if (-not $baselineApp.HasExited) { [void]$baselineApp.CloseMainWindow(); if (-not $baselineApp.WaitForExit(5000)) { throw 'Baseline did not close.' } } }
    if (-not (Test-Path -LiteralPath (Join-Path $libraryDirectory 'library.db'))) { throw 'Baseline did not create its library.' }
    Set-Content -LiteralPath $preferences -Value '{"GettingStartedCompleted":true,"TouchMode":true,"DarkTheme":true}'
    $retainedBeforeUpgrade = @($sentinel, $preferences, (Join-Path $libraryDirectory 'library.db')) | ForEach-Object { [pscustomobject]@{ Path=$_; Hash=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } }
}
Run-Setup $installer $arguments
if ($UpgradeFrom) {
    foreach ($retained in $retainedBeforeUpgrade) {
        if ((Get-FileHash -LiteralPath $retained.Path -Algorithm SHA256).Hash -ne $retained.Hash) { throw "Upgrade changed retained data: $($retained.Path)" }
    }
}
$entry = Get-ItemProperty -LiteralPath $registration
if ($entry.DisplayVersion -ne $Version -or $entry.InstallLocation.TrimEnd('\') -ne $installDirectory) { throw 'Installer registration does not match isolated test.' }
if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut missing.' }
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
if ($link.TargetPath -ne (Join-Path $installDirectory 'Ceeker.App.exe')) { throw 'Shortcut points outside isolated install.' }
$payload = Get-Content -LiteralPath (Join-Path $installDirectory 'manifest.json') -Raw | ConvertFrom-Json
foreach ($item in $payload) {
    $candidate = [IO.Path]::GetFullPath((Join-Path $installDirectory $item.path))
    if (-not $candidate.StartsWith($installDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escaped test install.' }
    if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $item.sha256) { throw "Installed payload mismatch: $($item.path)" }
}
$app = Start-Process -FilePath (Join-Path $installDirectory 'Ceeker.App.exe') -ArgumentList '--library',('"' + (Join-Path $libraryDirectory 'library.db') + '"') -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500; $app.Refresh()
        if ($app.HasExited) { throw "Installed app exited $($app.ExitCode)" }
        if ($app.MainWindowHandle -ne [IntPtr]::Zero -and $app.Responding) { $ready = $true; break }
    }
    if (-not $ready) { throw 'Installed app did not expose a responding window.' }
} finally { $app.Refresh(); if (-not $app.HasExited) { [void]$app.CloseMainWindow(); if (-not $app.WaitForExit(5000)) { throw 'Installed test app did not close.' } } }
if ($UpgradeFrom) {
    $saved = Get-Content -LiteralPath $preferences -Raw | ConvertFrom-Json
    if (-not $saved.TouchMode -or -not $saved.DarkTheme -or -not $saved.GettingStartedCompleted) { throw 'Upgraded launch did not retain saved preferences.' }
}
# Reinstall in the same directory exercises replacement without wiping preferences.
Run-Setup $installer $arguments
if (-not (Test-Path -LiteralPath (Join-Path $libraryDirectory 'library.db'))) { throw 'Saved library missing after reinstall.' }
$uninstaller = Join-Path $installDirectory 'unins000.exe'
if (-not ([IO.Path]::GetFullPath($uninstaller)).StartsWith($testRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstaller escaped test root.' }
Run-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $testRoot 'uninstall.log') + '"'))
if (Test-Path -LiteralPath $registration) { throw 'Uninstall registration remains.' }
if (Test-Path -LiteralPath $shortcut) { throw 'Start menu shortcut remains.' }
if (Test-Path -LiteralPath (Join-Path $installDirectory 'Ceeker.App.exe')) { throw 'App payload remains after uninstall.' }
if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash -or -not (Test-Path -LiteralPath $preferences) -or -not (Test-Path -LiteralPath (Join-Path $libraryDirectory 'library.db'))) { throw 'Uninstall changed retained data.' }
$report = [ordered]@{ checkedUtc=[DateTime]::UtcNow; installer=$installer; sha256=$expected; verifiedFiles=$payload.Count; testRoot=$testRoot; install='Passed per-user registration and payload'; launch='Responding window'; reinstall='Passed'; uninstall='Payload and registration removed; isolated library/preferences/original retained'; limitations='Development machine only; unsigned; no clean-machine or interactive wizard validation' }
if ($UpgradeFrom) { $report.upgrade = "$UpgradeFrom to $Version passed; library/preferences/original hashes retained before new-version launch" }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'artifacts/installer-verification.json')
$report | ConvertTo-Json





