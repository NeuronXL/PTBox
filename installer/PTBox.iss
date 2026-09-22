; Build with scripts/package.ps1. Always package a clean publish, never the personal portable folder.
#ifndef AppVersion
  #define AppVersion "1.1.2"
#endif
#ifndef PublishDir
  #error PublishDir is required (clean self-contained publish directory).
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{84089F79-568E-4B87-A01C-9FE490CAB973}
AppName=PTBox
AppVersion={#AppVersion}
AppVerName=PTBox {#AppVersion}
AppPublisher=PTBox
VersionInfoVersion={#AppVersion}
VersionInfoDescription=PTBox 客厅大屏 Launcher 安装程序
DefaultDirName={localappdata}\Programs\PTBox
DefaultGroupName=PTBox
DisableProgramGroupPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
WizardStyle=modern dark
WizardSizePercent=110
SetupIconFile=..\src\PTBox.Launcher\Assets\Brand\ptbox.ico
UninstallDisplayIcon={app}\PTBox.Launcher.exe
OutputDir={#OutputDir}
OutputBaseFilename=PTBox-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupMutex=PTBox.Setup
UninstallDisplayName=PTBox 客厅大屏 Launcher
DisableWelcomePage=no
DisableDirPage=no
UsePreviousTasks=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Messages]
WelcomeLabel2=即将在您的电脑上安装 PTBox 客厅大屏 Launcher。%n%n已包含运行环境，无需另外安装 .NET。升级会保留已有应用和设置。%n%n安装前请从 PTBox 的电源菜单选择“退出 Launcher”。
FinishedLabel=PTBox 已安装完成。%n%n可从开始菜单打开，使用方向键和确认键操作。卸载时将保留个人配置，便于以后重新安装。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "ptbox.install"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "Config\*,Logs\*,Cache\*,*.pdb,*.bak,*.broken"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\Config\config.json"; DestDir: "{app}\Config"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\PTBox"; Filename: "{app}\PTBox.Launcher.exe"; WorkingDir: "{app}"
Name: "{group}\卸载 PTBox"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PTBox"; Filename: "{app}\PTBox.Launcher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\PTBox.Launcher.exe"; Description: "启动 PTBox"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Remove autorun only when it points at this installation, preserving other copies. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PTBox.Launcher', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\PTBox.Launcher.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PTBox.Launcher');
  end;
end;
