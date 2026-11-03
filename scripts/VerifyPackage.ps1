param([string]$Version = '0.62.0-preview.1', [switch]$Launch, [ValidateSet('Full','Standard')][string]$Edition = 'Full')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Invalid version.' }
$workspace = Split-Path -Parent $PSScriptRoot
$editionSuffix = if ($Edition -eq 'Standard') { '-Standard' } else { '' }
$name = "Ceeker-$Version-win-x64$editionSuffix"
$archivePath = Join-Path $workspace "artifacts/releases/$name.zip"
$expectedHash = ((Get-Content -LiteralPath "$archivePath.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'ZIP checksum mismatch.' }
$verificationRoot = Join-Path $workspace ("artifacts/package-check-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationRoot | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($entry in $archive.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $verificationRoot $entry.FullName))
        if (-not $target.StartsWith($verificationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe ZIP entry.' }
    }
} finally { $archive.Dispose() }
[IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $verificationRoot)
$packageDirectory = Join-Path $verificationRoot $name
$manifest = Get-Content -LiteralPath (Join-Path $packageDirectory 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $path = [IO.Path]::GetFullPath((Join-Path $packageDirectory $entry.path))
    if (-not $path.StartsWith($packageDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe manifest path.' }
    if ((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Payload mismatch: $($entry.path)" }
}
if ((Get-ChildItem -LiteralPath $packageDirectory -File -Recurse).Count -ne $manifest.Count + 1) { throw 'Unexpected payload files.' }
foreach ($required in @('Ceeker.App.exe', 'Ceeker.App.pri', 'App.xbf', 'MainWindow.xbf', 'coreclr.dll', 'hostfxr.dll', 'Microsoft.UI.Xaml.dll', 'START-HERE.md', 'performance.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $required))) { throw "Missing package component: $required" }
}
foreach ($required in @('tessdata/eng.traineddata','tessdata/eng_best.traineddata','x64/tesseract50.dll','x64/leptonica-1.82.0.dll','licenses/Tesseract-wrapper.txt','licenses/Tesseract-model.txt','licenses/Tesseract-engine.txt','licenses/Leptonica.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $required))) { throw "Missing OCR component: $required" }
}
if ([version]($Version -split '-')[0] -ge [version]'0.61.0') {
    foreach ($required in @('Ceeker.Actions.exe','Ceeker.Actions.dll','Ceeker.Actions.deps.json','Ceeker.Actions.runtimeconfig.json','UI-0.61.md','WINDOWS-ACTIONS.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $required))) { throw "Missing desktop component: $required" }
    }
    & (Join-Path $packageDirectory 'Ceeker.Actions.exe') --help | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Packaged actions failed to launch.' }
}
if ($Edition -eq 'Standard' -and (Test-Path -LiteralPath (Join-Path $packageDirectory 'Models'))) { throw 'Standard package unexpectedly includes AI models.' }
$launchResult = 'Not requested'
foreach ($required in @('UglyToad.PdfPig.dll','UglyToad.PdfPig.Core.dll','UglyToad.PdfPig.DocumentLayoutAnalysis.dll','licenses/PdfPig.txt','licenses/PdfPig-NOTICES.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $required))) { throw "Missing PDF component: $required" }
}
if ($Launch) {
    $testLibrary = Join-Path $verificationRoot 'launch-library.db'
    $process = Start-Process -FilePath (Join-Path $packageDirectory 'Ceeker.App.exe') -ArgumentList '--library',('"' + $testLibrary + '"') -WorkingDirectory $packageDirectory -WindowStyle Hidden -PassThru
    try {
        $windowFound = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Milliseconds 500
            $process.Refresh()
            if ($process.HasExited) { throw "Packaged app exited early: $($process.ExitCode)" }
            if ($process.MainWindowHandle -ne [IntPtr]::Zero -and $process.Responding) { $windowFound = $true; break }
        }
        if (-not $windowFound) { throw 'Packaged app did not expose a responding window within 10 seconds.' }
        Start-Sleep -Seconds 2
        $process.Refresh()
        if ($process.HasExited -or -not $process.Responding) { throw 'Packaged app failed after its window appeared.' }
        $launchResult = 'Responding main window on development machine; no clean-machine or UI interaction validation'
    } finally {
        $process.Refresh()
        if (-not $process.HasExited) { [void]$process.CloseMainWindow(); [void]$process.WaitForExit(5000) }
    }
}
$report = [ordered]@{ checkedUtc = [DateTime]::UtcNow; archive = $archivePath; sha256 = $expectedHash; verifiedFiles = $manifest.Count; extractedDirectory = $packageDirectory; launch = $launchResult }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'artifacts/package-verification.json') -Encoding utf8
$report | ConvertTo-Json




















