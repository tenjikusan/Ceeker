#ifndef AppVersion
  #define AppVersion "0.29.0-preview.1"
#endif
#ifndef PayloadDir
  #error PayloadDir must point to a verified published package
#endif
#ifndef OutputSuffix
  #define OutputSuffix ""
#endif
[Setup]
AppId={{4B6F1C7D-85B5-493A-B57E-96C4C6D40ED1}
AppName=Ceeker
AppVersion={#AppVersion}
AppPublisher=Ceeker
DefaultDirName={localappdata}\Programs\Wormy
DefaultGroupName=Ceeker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\releases
OutputBaseFilename=Ceeker-{#AppVersion}-Setup-x64{#OutputSuffix}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Ceeker.App.exe
CloseApplications=yes
CloseApplicationsFilter=Ceeker.App.exe,Wormy.App.exe
RestartApplications=no
SetupLogging=yes
SetupIconFile=../src/Wormy.App/Assets/ceeker.ico
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\Ceeker"; Filename: "{app}\Ceeker.App.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Ceeker"; Filename: "{app}\Ceeker.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Ceeker.App.exe"; Description: "Launch Ceeker"; Flags: nowait postinstall skipifsilent
; No UninstallDelete rules: the saved library under LocalAppData\Wormy
; and all connected original folders are deliberately retained.




[InstallDelete]
Type: files; Name: "{userprograms}\Wormy.lnk"
Type: files; Name: "{userdesktop}\Wormy.lnk"
Type: files; Name: "{app}\Wormy.App.exe"
Type: files; Name: "{app}\Wormy.App.dll"
Type: files; Name: "{app}\Wormy.App.pri"
Type: files; Name: "{app}\Wormy.App.deps.json"
Type: files; Name: "{app}\Wormy.App.runtimeconfig.json"
