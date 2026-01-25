[Setup]
AppName=Church Display App
AppVersion=1.0.0
AppPublisher=Church Media Ministry
AppPublisherURL=https://
AppSupportURL=https://
AppUpdatesURL=https://
AppId={{B7A6A8E4-1D88-4B29-8D44-5E3B6B6C3B5D}}
DefaultDirName={pf}\ChurchDisplayApp
DefaultGroupName=Church Display App
AllowNoIcons=no
LicenseFile=.\LICENSE.txt
OutputDir=.\bin\Installer
OutputBaseFilename=ChurchDisplayApp-1.0.0-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\ChurchDisplayApp.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commonprograms}\Church Display App\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"

[Run]
Filename: "{app}\ChurchDisplayApp.exe"; Description: "{cm:LaunchProgram,Church Display App}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
