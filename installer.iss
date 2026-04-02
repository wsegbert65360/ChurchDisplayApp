[Setup]
AppId={{B7A6A8E4-1D88-4B29-8D44-5E3B6B6C3B5D}
AppName=Church Display App
AppVersion=1.2
AppPublisher=Church Media Ministry
DefaultDirName={autopf}\Church Display App
DefaultGroupName=Church Display App
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=ChurchDisplayApp_Installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Request admin elevation so Windows shows a UAC prompt during install.
; This lets us create firewall rules without the app needing to run as admin.
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"; Tasks: desktopicon

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
