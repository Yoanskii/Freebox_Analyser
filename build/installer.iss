#define MyAppName "Free Network Monitor"
#define MyAppVersion "1.0.0"
[Setup]
AppId={{0B6B5D3C-F178-4A14-B982-10196BDB28F2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Free Network Monitor
DefaultDirName={localappdata}\Programs\FreeNetworkMonitor
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\installer
OutputBaseFilename=FreeNetworkMonitor-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\FreeNetworkMonitor.App.exe
CloseApplications=yes
[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\FreeNetworkMonitor.App.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\FreeNetworkMonitor.App.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\FreeNetworkMonitor.App.exe"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "FreeNetworkMonitor"; Flags: uninsdeletevalue
; Ne jamais supprimer %LOCALAPPDATA%\FreeNetworkMonitor à la désinstallation.
