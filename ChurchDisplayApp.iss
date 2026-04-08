[Setup]
AppName=Church Display App
AppVersion=1.3
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
OutputBaseFilename=ChurchDisplayApp-1.3-Setup
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

[Code]
// Check whether the Visual C++ 2015-2022 Redistributable (x64) is installed.
// Returns True if a matching version is found in the registry.
function IsVCRedistInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  // The VC++ 2015-2022 redistributable all share the same major version (14).
  // Checking the latest minimum version covers all of them.
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64') or
            RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64');
end;

// Download and install VC++ Redistributable if missing.
// Returns True on success or if already installed.
function InstallVCRedist(): Boolean;
var
  ResultCode: Integer;
  TempPath: String;
  Url: String;
begin
  Result := False;

  if IsVCRedistInstalled() then
  begin
    Result := True;
    Exit;
  end;

  Url := 'https://aka.ms/vs/17/release/vc_redist.x64.exe';
  TempPath := ExpandConstant('{tmp}\vc_redist.x64.exe');

  // Inform the user
  if MsgBox('Church Display App requires the Microsoft Visual C++ Redistributable (x64), which is not detected on this system.' + #13#10 + #13#10 +
            'Click OK to download and install it now (internet connection required).' + #13#10 +
            'Click Cancel to abort the installation.',
            mbConfirmation, MB_OKCANCEL) <> IDOK then
    Exit;

  WizardForm.StatusLabel.Caption := 'Downloading Visual C++ Redistributable...';
  try
    if not DownloadFile(Url, TempPath) then
    begin
      MsgBox('Failed to download the Visual C++ Redistributable. Please install it manually from:' + #13#10 + Url,
             mbError, MB_OK);
      Exit;
    end;
  except
    MsgBox('Failed to download the Visual C++ Redistributable. Please install it manually from:' + #13#10 + Url,
           mbError, MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Installing Visual C++ Redistributable...';
  try
    if Exec(TempPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode = 0 then
        Result := True
      else if ResultCode = 3010 then
      begin
        // 3010 = success but reboot required
        Result := True;
      end
      else
        MsgBox('Visual C++ Redistributable installer returned error code ' + IntToStr(ResultCode) + '.' + #13#10 +
               'You may need to install it manually from: ' + #13#10 + Url,
               mbError, MB_OK);
    end;
  except
    MsgBox('Failed to run the Visual C++ Redistributable installer. Please install it manually from:' + #13#10 + Url,
           mbError, MB_OK);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

[Files]
Source: "bin\Publish\win-x64\ChurchDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\Church Display App\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"
Name: "{commondesktop}\Church Display App"; Filename: "{app}\ChurchDisplayApp.exe"

[Run]
; Install VC++ Redistributable if missing (installer is elevated)
Filename: "code:InstallVCRedist"; Flags: runhidden

; Create firewall rules during install (installer is elevated)
; Ports must match AppConstants.Network.RemoteControlPortPreferred (8088) and Fallback (8090)
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote"" dir=in action=allow protocol=TCP localport=8088 profile=any"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""ChurchDisplayApp Remote Fallback"" dir=in action=allow protocol=TCP localport=8090 profile=any"; Flags: runhidden
; Launch app after install
Filename: "{app}\ChurchDisplayApp.exe"; Description: "{cm:LaunchProgram,Church Display App}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Clean up firewall rules when uninstalling
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote"""; Flags: runhidden; RunOnceId: "UninstallFirewall1"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ChurchDisplayApp Remote Fallback"""; Flags: runhidden; RunOnceId: "UninstallFirewall2"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
