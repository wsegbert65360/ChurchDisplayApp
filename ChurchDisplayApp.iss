#define MyAppVersion GetFileVersion("bin\Publish\win-x64\ChurchDisplayApp.exe")

[Setup]
AppName=Church Display App
AppVersion={#MyAppVersion}
AppPublisher=Church Media Ministry
AppPublisherURL=https://github.com/wsegbert65360/ChurchDisplayApp
AppSupportURL=https://github.com/wsegbert65360/ChurchDisplayApp/issues
AppUpdatesURL=https://github.com/wsegbert65360/ChurchDisplayApp/releases
AppId={{B7A6A8E4-1D88-4B29-8D44-5E3B6B6C3B5D}}
DefaultDirName={autopf}\ChurchDisplayApp
DefaultGroupName=Church Display App
AllowNoIcons=no
LicenseFile=.\LICENSE.txt
OutputDir=.\bin\Installer
OutputBaseFilename=ChurchDisplayApp-{#MyAppVersion}-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\ChurchDisplayApp.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Code]
// Check whether the Visual C++ 2015-2022 Redistributable (x64) is installed.
// Returns True if a matching version is found in the registry.
function IsVCRedistInstalled(): Boolean;
var
  Major, Minor: Cardinal;
begin
  Result := False;
  // VC++ 2015-2022 redistributables all write to this key.
  // We require Major=14, Minor>=40 (VS 2022 baseline).
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Major', Major) then
  begin
    if Major >= 14 then
    begin
      if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', Minor) then
        Result := (Major > 14) or (Minor >= 40);
    end;
  end;
end;

// Download and install VC++ Redistributable if missing.
// Uses PowerShell (always available on modern Windows) to download and run — no plugins needed.
procedure InstallVCRedistIfNeeded();
var
  ResultCode: Integer;
  TempPath: String;
  Url: String;
  PsParams: String;
begin
  if IsVCRedistInstalled() then
    Exit;

  Url := 'https://aka.ms/vs/17/release/vc_redist.x64.exe';
  TempPath := ExpandConstant('{tmp}\vc_redist.x64.exe');

  if MsgBox('Church Display App requires the Microsoft Visual C++ Redistributable (x64), which is not detected on this system.' + #13#10 + #13#10 +
            'Click OK to download and install it now (internet connection required).' + #13#10 +
            'Click Cancel to skip (the app may not work correctly).',
            mbConfirmation, MB_OKCANCEL) <> IDOK then
    Exit;

  WizardForm.StatusLabel.Caption := 'Downloading Visual C++ Redistributable...';
  PsParams := '-NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri ''' + Url + ''' -OutFile ''' + TempPath + '''"';

  if not Exec('powershell.exe', PsParams, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    MsgBox('Failed to download the Visual C++ Redistributable.' + #13#10 + #13#10 +
           'Please install it manually from:' + #13#10 + Url,
           mbError, MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Installing Visual C++ Redistributable...';
  Exec(TempPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox('Visual C++ Redistributable installer returned error code ' + IntToStr(ResultCode) + '.' + #13#10 + #13#10 +
           'Please install it manually from:' + #13#10 + Url,
           mbError, MB_OK);
  end;
end;

// Called automatically during the install process at each step.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Run VC++ check at ssInstall (before files are copied) so the runtime
  // is guaranteed to be present before the app is launched for the first time.
  if CurStep = ssInstall then
    InstallVCRedistIfNeeded();
end;

[Files]
Source: "bin\Publish\win-x64\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\Church Display App\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"

[Run]
; Remove any existing rules first so reinstall doesn't create duplicates
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote Fallback"""; Flags: runhidden
; Create firewall rules during install (installer is elevated)
; Ports must match AppConstants.Network.RemoteControlPortPreferred (8088) and Fallback (8090)
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote"" dir=in action=allow protocol=TCP localport=8088 profile=any"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote Fallback"" dir=in action=allow protocol=TCP localport=8090 profile=any"; Flags: runhidden
; Launch app after install
Filename: "{app}\ChurchDisplayApp.exe"; Description: "{cm:LaunchProgram,Church Display App}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Clean up firewall rules when uninstalling
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote"""; Flags: runhidden; RunOnceId: "UninstallFirewall1"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote Fallback"""; Flags: runhidden; RunOnceId: "UninstallFirewall2"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
