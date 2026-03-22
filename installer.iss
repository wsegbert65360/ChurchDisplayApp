[Setup]
AppId={{E68B6E32-0A1D-47C5-8B4E-2E4774F1CA92}
AppName=Church Display App
AppVersion=1.1
AppPublisher=Church Display
DefaultDirName={autopf}\Church Display App
DefaultGroupName=Church Display App
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=ChurchDisplayApp_Installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ChurchDisplayApp.exe"; Description: "{cm:LaunchProgram,Church Display App}"; Flags: nowait postinstall skipifsilent
