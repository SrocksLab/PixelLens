#define AppName "PixelLens · 像素之眼"
#ifndef AppVersion
#define AppVersion "1.0.0"
#endif
#define AppExeName "UICompare.exe"
#ifndef InstallerAppId
#define InstallerAppId "{{B7925F07-0C7A-4A4C-89D7-123456789001}}"
#endif
#ifndef AppPayload
#define AppPayload "..\..\output\releases\v" + AppVersion + "\app\UICompare.exe"
#endif
[Setup]
AppId={#InstallerAppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=PixelLens
DefaultDirName={localappdata}\Programs\PixelLens
DefaultGroupName=像素之眼
PrivilegesRequired=lowest
OutputDir=..\..\output\releases\v{#AppVersion}\engine
OutputBaseFilename=PixelLens-Engine-v{#AppVersion}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
CloseApplications=yes
[Files]
Source: "{#AppPayload}"; DestDir: "{app}"; Flags: ignoreversion
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce
#ifndef VerificationOnly
[Icons]
Name: "{userdesktop}\像素之眼"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{group}\像素之眼"; Filename: "{app}\{#AppExeName}"
#endif
[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动像素之眼"; Flags: nowait postinstall skipifsilent

[LangOptions]
DialogFontName=Microsoft YaHei UI
DialogFontSize=9
[Messages]
SetupWindowTitle=安装 - %1
UninstallAppFullTitle=卸载 %1
ButtonBack=上一步
ButtonNext=下一步
ButtonInstall=安装
ButtonCancel=取消
ButtonFinish=完成
ButtonBrowse=浏览…
ButtonWizardBrowse=浏览…
WelcomeLabel1=欢迎安装 [name]
WelcomeLabel2=将安装 [name/ver]。%n%n安装后可通过桌面快捷方式打开。
ClickNext=点击“下一步”继续，或点击“取消”退出安装。
WizardSelectDir=选择安装位置
SelectDirDesc=将 [name] 安装到哪里？
SelectDirLabel3=程序将安装到以下文件夹。
SelectDirBrowseLabel=点击“下一步”继续，或点击“浏览”选择其他文件夹。
WizardSelectTasks=快捷方式
SelectTasksDesc=选择需要的快捷方式
SelectTasksLabel2=点击“下一步”继续。
WizardReady=准备安装
ReadyLabel1=即将安装 [name]。
ReadyLabel2a=点击“安装”开始，或点击“上一步”检查设置。
WizardInstalling=正在安装
InstallingLabel=正在安装 [name]，请稍候。
FinishedHeadingLabel=安装完成
FinishedLabelNoIcons=[name] 已安装完成。
FinishedLabel=[name] 已安装完成，可通过快捷方式打开。
ClickFinish=点击“完成”退出安装。
StatusExtractFiles=正在复制文件…
ExitSetupTitle=退出安装
ExitSetupMessage=安装尚未完成。确定退出安装吗？
ConfirmUninstall=确定卸载 %1 吗？
UninstalledAll=%1 已卸载完成。

[Code]
function StatusPath: String;
begin Result := ExpandConstant('{param:FLOWSTATUS|}'); end;
function CancelPath: String;
begin Result := ExpandConstant('{param:FLOWCANCEL|}'); end;
function InitializeSetup: Boolean;
begin
 Result := not ((CancelPath <> '') and FileExists(CancelPath));
end;
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
 if (CancelPath <> '') and FileExists(CancelPath) then begin Cancel := True; Confirm := False; end;
end;
procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
 if (StatusPath <> '') and (MaxProgress > 0) then
  SaveStringToFile(StatusPath, IntToStr(CurProgress) + '/' + IntToStr(MaxProgress), False);
 if (CancelPath <> '') and FileExists(CancelPath) then begin
  { The native cancellation path requires a visible/enabled form even in very-silent mode.
    Place it outside all desktop surfaces before enabling the normal rollback path. }
  WizardForm.Left := -30000; WizardForm.Top := -30000;
  WizardForm.Visible := True; WizardForm.Enabled := True;
  WizardForm.CancelButton.Enabled := True;
  WizardForm.Close;
 end;
end;
