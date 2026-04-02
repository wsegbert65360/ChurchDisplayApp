[Setup]
AppName=Church Display App
AppVersion=1.2
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
OutputBaseFilename=ChurchDisplayApp-1.2-Setup
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
Source: "bin\Publish\win-x64\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\Church Display App\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"

[Run]
; Create firewall rules during install (installer is elevated)
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote"" dir=in action=allow protocol=TCP localport=80 profile=any"; Flags: runhidden ignoreerrors;
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote Fallback"" dir=in action=allow protocol=TCP localport=8088 profile=any"; Flags: runhidden ignoreerrors;
; Launch app after install
Filename: "{app}\ChurchDisplayApp.exe"; Description: "{cm:LaunchProgram,Church Display App}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Clean up firewall rules when uninstalling
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote"""; Flags: runhidden ignoreerrors; RunOnceId: "UninstallFirewall1"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote Fallback"""; Flags: runhidden ignoreerrors; RunOnceId: "UninstallFirewall2"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
