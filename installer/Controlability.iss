; Controlability installer (Inno Setup 6.3 or newer).
;
; Normally built by build-release.ps1, which passes:
;   /DAppVersion=1.0.0-rc1  /DAppFileVersion=1.0.0.0  /DPublishDir=<abs path to artifacts\publish>  /DOutputDir=<abs path to artifacts>
;
; Drivers (ViGEmBus is BSD-3-Clause, HidHide is MIT; both allow redistribution):
;   * If the official installers are in installer\redist\ under the exact names below, they are bundled.
;   * Otherwise they are downloaded from the official GitHub releases during setup, only if they are missing.
;   * To pin the download, fill in the SHA-256 defines below (empty = not checked).

#if VER < EncodeVer(6, 3, 0)
  #error Inno Setup 6.3 or newer is required.
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0-rc1"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "1.0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Controlability"
#define AppExe "Controlability.exe"
#define AppUrl "https://github.com/SinisterRedTrue/Controlability"

#define ViGEmFile "ViGEmBus_1.22.0_x64_x86_arm64.exe"
#define ViGEmUrl "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
#define ViGEmSha256 ""
#define HidHideFile "HidHide_1.5.230_x64.exe"
#define HidHideUrl "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"
#define HidHideSha256 ""

#ifexist "redist\" + ViGEmFile
  #define ViGEmBundled "True"
#else
  #define ViGEmBundled "False"
#endif
#ifexist "redist\" + HidHideFile
  #define HidHideBundled "True"
#else
  #define HidHideBundled "False"
#endif

[Setup]
AppId={{B3F0C2A4-6D1E-4C52-9A7B-0C0A1B1E7D10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Controlability
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
VersionInfoVersion={#AppFileVersion}
VersionInfoProductVersion={#AppFileVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Admin is needed for the driver installers.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\LICENSE
SetupIconFile=..\src\Controlability.App\Resources\Icons\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=Controlability-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ChangesEnvironment=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start {#AppName} with Windows"; GroupDescription: "Startup:"; Flags: unchecked
Name: "installvigem"; Description: "Install the ViGEmBus driver (needed for the virtual Xbox controller)"; GroupDescription: "Drivers (not found on this PC):"; Check: NeedViGEm
Name: "installhidhide"; Description: "Install the HidHide driver (hides the real controller from games; a restart is usually needed)"; GroupDescription: "Drivers (not found on this PC):"; Check: NeedHidHide

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion
#if ViGEmBundled == "True"
Source: "redist\{#ViGEmFile}"; DestDir: "{tmp}"; Flags: dontcopy
#endif
#if HidHideBundled == "True"
Source: "redist\{#HidHideFile}"; DestDir: "{tmp}"; Flags: dontcopy
#endif

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value and format the app writes itself (RegistryAutostart), so the Settings toggle and this task agree.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; A running copy would hold its controllers hidden and lock the files; the cleanup afterwards unhides from the journal.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden waituntilterminated; RunOnceId: "KillApp"
Filename: "{app}\{#AppExe}"; Parameters: "--cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup"

[Code]
const
  ViGEmServiceKey = 'SYSTEM\CurrentControlSet\Services\ViGEmBus';
  HidHideServiceKey = 'SYSTEM\CurrentControlSet\Services\HidHide';
  ViGEmBundled = {#ViGEmBundled};
  HidHideBundled = {#HidHideBundled};

var
  DownloadPage: TDownloadWizardPage;
  ViGEmInstaller: String;
  HidHideInstaller: String;
  RestartNeeded: Boolean;

function NeedViGEm: Boolean;
begin
  Result := not RegKeyExists(HKLM, ViGEmServiceKey);
end;

function NeedHidHide: Boolean;
begin
  Result := not RegKeyExists(HKLM, HidHideServiceKey);
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

{ Returns the path of the installer for a driver the user ticked, or '' if it was not ticked or could not be fetched. }
function PrepareDriver(const Task, FileName, Url, Sha256: String; Bundled: Boolean): String;
begin
  Result := '';
  if not WizardIsTaskSelected(Task) then
    Exit;

  if Bundled then
  begin
    ExtractTemporaryFile(FileName);
    Result := ExpandConstant('{tmp}\' + FileName);
  end
  else
  begin
    DownloadPage.Clear;
    DownloadPage.Add(Url, FileName, Sha256);
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        Result := ExpandConstant('{tmp}\' + FileName);
      except
        if not DownloadPage.AbortedByUser then
          Log('Download of ' + FileName + ' failed: ' + GetExceptionMessage);
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then
    Exit;

  ViGEmInstaller := PrepareDriver('installvigem', '{#ViGEmFile}', '{#ViGEmUrl}', '{#ViGEmSha256}', ViGEmBundled);
  HidHideInstaller := PrepareDriver('installhidhide', '{#HidHideFile}', '{#HidHideUrl}', '{#HidHideSha256}', HidHideBundled);

  if (WizardIsTaskSelected('installvigem') and (ViGEmInstaller = '')) or
     (WizardIsTaskSelected('installhidhide') and (HidHideInstaller = '')) then
  begin
    { The app starts without the drivers and shows an "Install driver" banner (fail safe), so offering to go on is fine. }
    Result := SuppressibleMsgBox(
      'A driver installer could not be downloaded (are you offline?).' + #13#10 + #13#10 +
      'Continue without it? Controlability will start and show an "Install driver" banner; ' +
      'you can install the driver later from the links in the app.',
      mbConfirmation, MB_YESNO, IDYES) = IDYES;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { An upgrade over a running copy: close it, otherwise its files are locked. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure RunDriverInstaller(const Name, Path: String);
var
  ResultCode: Integer;
begin
  if Path = '' then
    Exit;

  WizardForm.StatusLabel.Caption := 'Installing ' + Name + '...';
  if not Exec(Path, '/quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log(Name + ' installer did not start: ' + SysErrorMessage(ResultCode));
    SuppressibleMsgBox(Name + ' could not be installed. Install it later from its website; Controlability still starts.', mbError, MB_OK, IDOK);
  end
  else if (ResultCode = 3010) or (ResultCode = 1641) then
  begin
    { 3010 / 1641: installed, restart required. }
    RestartNeeded := True;
  end
  else if ResultCode <> 0 then
  begin
    Log(Name + ' installer exit code ' + IntToStr(ResultCode));
    SuppressibleMsgBox(Name + ' reported error code ' + IntToStr(ResultCode) + '. Install it later from its website; Controlability still starts.', mbError, MB_OK, IDOK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RunDriverInstaller('ViGEmBus', ViGEmInstaller);
    RunDriverInstaller('HidHide', HidHideInstaller);
  end;
end;

function NeedRestart: Boolean;
begin
  Result := RestartNeeded;
end;

function RemoveSettingsRequested: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/REMOVESETTINGS') = 0 then
      Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { The user's profiles and settings stay unless they ask for them to go (a silent uninstall needs /REMOVESETTINGS). }
  if RemoveSettingsRequested or
     (not UninstallSilent and
      (MsgBox('Also remove my settings, profiles and logs?' + #13#10 + #13#10 +
              'Choose No to keep them for a later reinstall.',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES)) then
  begin
    DelTree(ExpandConstant('{userappdata}\Controlability'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\Controlability'), True, True, True);
  end;
end;
