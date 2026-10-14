# Ceeker

Ceeker is a Windows desktop file library with offline search, document/image OCR, previews, exact duplicate review, folder-scoped scans and cleanup suggestions.

This source snapshot is version **0.60.0-preview.2**, prepared for GitHub. It is a preview release; native workflow, clean-machine, DPI and physical-touch validation remain pending. Signed updates and checkout are intentionally inactive. The project has not been assigned a new open-source license by this export; existing third-party notices are retained.

## Requirements

- Windows x64, Windows 10 build 19041 or later.
- .NET 10 SDK and PowerShell 7 (`pwsh`).
- Git and Git LFS for the included AI/OCR model binaries.
- Inno Setup 6.7+ to compile an installer; pass its ISCC.exe path to BuildInstaller.ps1.

## Build and check

```powershell
git lfs install
git lfs pull
dotnet build src/Wormy.App/Wormy.App.csproj -c Release -p:Platform=x64
dotnet run --project tests/Wormy.Core.Checks -c Release
```

The product name is Ceeker. Internal Wormy project names, data location and installation AppId remain for compatibility with existing users.

## Package

Use a fresh version for each release:

```powershell
pwsh -NoProfile -File scripts/Package.ps1 -Version 0.61.0-preview.1
pwsh -NoProfile -File scripts/BuildInstaller.ps1 -Version 0.61.0-preview.1 -Compiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Packaging is self-contained and includes offline models. It uses the retained benchmark fixture report in build-support/performance.json; this is a historical report, not a new benchmark. Generated outputs appear in artifacts/ and are ignored by Git. Publish installers separately as GitHub Release assets.

See [GitHub upload instructions](PUBLISH-GITHUB.md), [quick start](docs/QUICKSTART.md), [third-party notices](docs/THIRD-PARTY.md) and [current validation](docs/VALIDATION-0.60.md).

Private credentials/signing keys, user libraries, test datasets, installers, temporary outputs and existing Git history are excluded from this folder.
