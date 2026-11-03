# Smaller Ceeker installer

The Standard installer includes normal filename/folder/date/size search, tags, indexed document search, OCR, previews, duplicates, cleanup review, Quick Search, the tray panel and the desktop improvements. It excludes the large MiniLM/CLIP model files. All Standard features work offline.

AI similarity search is optional. Install the separate Ceeker AI Pack into the folder containing your installed Ceeker.App.exe, then restart Ceeker and enable Offline AI text and photo search in Settings. The default application folder is `%LOCALAPPDATA%\Programs\Wormy` (the retained internal installation path). The AI Pack does not replace the application. It has its own Windows Apps entry and uninstaller in AiPackUninstall, separate from the main uninstaller, so you can remove the optional models independently. Installing the pack requires Ceeker to be closed. It adds model files, model licenses and its uninstall support only. The pack cannot install into a directory without Ceeker.App.exe. Uninstalling Standard does not remove the separately installed AI pack; remove that entry too when removing everything.

No automatic download is active: a release-hosting HTTPS URL has not been configured. The separate installer can be distributed manually alongside the Standard installer. Once installed, AI search stays offline. The previous full offline 0.61 installer remains available with the same models bundled.

Installing Standard over an existing Full installation retains already installed AI model files. This preserves existing functionality and avoids deleting optional user-installed components. Standard reduces the initial download; it does not automatically remove previously installed models. Its runtime libraries remain self-contained, supporting clean Windows installations without requiring a separate .NET download. Further runtime trimming needs compatibility testing and is not applied here.

Build with PowerShell 7:

```powershell
./scripts/Package.ps1 -Version 0.62.0-preview.1 -Edition Standard
./scripts/BuildInstaller.ps1 -Version 0.62.0-preview.1 -Edition Standard
./scripts/BuildAiPack.ps1 -Version 0.62.0-preview.1
```

Both downloads have SHA-256 sidecars. Packaging validates the AI source hashes, excludes Models from Standard, verifies the Standard payload manifest and launches its bundled CLI help before building Setup. Neither installer contains test fixture libraries.
