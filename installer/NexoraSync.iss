[Setup]
AppId={{A9F6E2A7-9D2E-4C17-AC21-6D4A6A2B12F0}
AppName=NEXORA SYNC
AppVersion=0.1.2
AppPublisher=NEXORA
DefaultDirName={autopf}\NEXORA SYNC
DefaultGroupName=NEXORA SYNC
OutputDir=..\artifacts
OutputBaseFilename=NexoraSync-Setup-0.1.2
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\NexoraSync.App\nexora.ico
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\NexoraSync.exe

[Files]
Source: "..\publish\NexoraSync.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\NEXORA SYNC"; Filename: "{app}\NexoraSync.exe"
Name: "{autodesktop}\NEXORA SYNC"; Filename: "{app}\NexoraSync.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Run]
Filename: "{app}\NexoraSync.exe"; Description: "Abrir NEXORA SYNC"; Flags: postinstall nowait skipifsilent