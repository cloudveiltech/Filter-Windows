#define MyAppName "CloudVeil For Windows"
#define MyAppPublisher "CloudVeil"
#define MyAppURL "https://www.cloudveil.org/"
#define DownloadURL "https://manage.cloudveil.org/citadel/update/latest/%s%s"
#define LocalInstallerName "cv4w.exe"

[Setup]
AppName={#MyAppName}
AppVersion=1.0
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=CloudVeilWebInstaller
Compression=lzma
SolidCompression=yes
Uninstallable=no
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
SetupIconFile=appicon.ico

WizardStyle=modern
WizardSmallImageFile=CloudVeilWelcomeSmall.bmp
WizardImageFile=CloudVeilWelcome.bmp

[Messages]
WelcomeLabel2=This installer will download and install {#MyAppName} on your computer.%n%nPlease make sure you are connected to the Internet.

[Files]
Source: "appicon.ico"; Flags: dontcopy

[Code]

function GetParamValue(const ParamName: string; Default: string): string;
var
  I: Integer;
  Param, Key: string;
begin
  Result := Default;
  
  for I := 1 to ParamCount do
  begin
    Param := ParamStr(I);
    
    if (Length(Param) > 0) and (Param[1] in ['/']) then
    begin
      Delete(Param, 1, 1);      
      if Pos('=', Param) > 0 then
      begin
        Key := Copy(Param, 1, Pos('=', Param) - 1);
        if CompareText(Key, ParamName) = 0 then
        begin
          Result := Copy(Param, Pos('=', Param) + 1, MaxInt);
          Break;
        end;
      end;
    end;
  end;
end;

var
  DownloadPage: TDownloadWizardPage;
  CancelPressed: Boolean;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := not CancelPressed;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  CancelPressed := True;
  Confirm := False;  
end;


procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function DownloadFile(Url, FileName: string): Boolean;
begin
  try
    DownloadPage.Clear;
    DownloadPage.Add(Url, FileName, '');
    DownloadPage.Show;
    try
      CancelPressed := False;
      DownloadPage.Download;
      Result := True;
    except
      if DownloadPage.AbortedByUser then
        Log('Aborted by user.')
      else
        SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;
end;


procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  InstallerPath: string;
  DownloadURL: string;
  UserIdParam, PlatformParam, UserIdHash: string;  
  I: Integer;
  InstallerParams: String;

begin
  if CurStep = ssInstall then
  begin
    InstallerPath := ExpandConstant('{tmp}\{#LocalInstallerName}');
    
    UserIdParam := GetParamValue('userid', '');
    UserIdHash := '';
    if Pos(':', UserIdParam) > 0 then
    begin
        UserIdHash := '/' + Copy(UserIdParam, Pos(':', UserIdParam) + 1, MaxInt);
    end;

  PlatformParam := 'cv4w-';
    case ProcessorArchitecture of
        paX86: PlatformParam := PlatformParam + 'x86';
        paX64: PlatformParam := PlatformParam + 'x64';
        paArm64: PlatformParam := PlatformParam + 'arm64';
    end;

    DownloadURL := Format('{#DownloadURL}', [PlatformParam, UserIdHash])

    if not DownloadFile(DownloadURL, '{#LocalInstallerName}') then
    begin
      MsgBox('Failed to download the installer. Please check your internet connection and try again.', mbError, MB_OK);
      WizardForm.Close;
      Exit;
    end;

    InstallerParams := '';
    for I := 1 to ParamCount do
    begin
        InstallerParams := InstallerParams + ' ' + ParamStr(I);
    end;
    
    if Exec(InstallerPath, InstallerParams, '', SW_SHOW, ewNoWait, ResultCode) then
    begin
      Log('Main installer launched successfully.');
    end
    else
    begin
      MsgBox('Failed to launch the installer. Error code: ' + IntToStr(ResultCode), mbError, MB_OK);
    end;
    
    Abort;
  end;
end;