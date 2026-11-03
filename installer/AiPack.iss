#ifndef PackVersion
  #error PackVersion is required
#endif
#ifndef SourceRoot
  #error SourceRoot is required
#endif
[Setup]
AppId=CeekerOptionalAiPack
AppName=Ceeker Optional AI Pack
AppVersion={#PackVersion}
AppPublisher=Ceeker
DefaultDirName={localappdata}\Programs\Wormy
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\releases
OutputBaseFilename=Ceeker-{#PackVersion}-AI-Pack-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes
UninstallFilesDir={app}\AiPackUninstall
CloseApplications=yes
CloseApplicationsFilter=Ceeker.App.exe
RestartApplications=no
SetupIconFile=../src/Wormy.App/Assets/ceeker.ico
[Files]
Source: "{#SourceRoot}\Models\*"; DestDir: "{app}\Models"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\licenses\CLIP-MIT.txt"; DestDir: "{app}\Models\licenses"; Flags: ignoreversion
Source: "{#SourceRoot}\licenses\MiniLM-Apache2.txt"; DestDir: "{app}\Models\licenses"; Flags: ignoreversion
[Code]
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not FileExists(ExpandConstant('{app}\Ceeker.App.exe')) then
  begin
    MsgBox('Choose the folder containing your installed Ceeker.App.exe. Install Ceeker first if it is not installed.', mbError, MB_OK);
    Result := False;
  end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not FileExists(ExpandConstant('{app}\Ceeker.App.exe')) then
    Result := 'Install Ceeker first, then select its application folder.';
end;
