; QuotaMonitor（AI 额度监控）安装包脚本（Inno Setup 6）
#define MyAppName "QuotaMonitor"
#define MyAppVersion "1.0.0"
#define MyAppExeName "QuotaMonitor.exe"

[Setup]
; 唯一标识（个人工具，固定即可）
AppId={{8F2B6C41-9A5E-4D3B-8C10-2E7F5A91B6D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=QuotaMonitor
DefaultDirName={localappdata}\QuotaMonitor
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 无需管理员权限（安装到当前用户目录）
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=QuotaMonitor-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\QuotaMonitor\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; 仅 64 位系统（Windows 10/11 x64）
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 卸载后清理：安装目录（用户数据在「我的文档」，天然保留）
UninstallDisplayName={#MyAppName}

[Languages]
; 使用仓库内自带的中文语言文件（CI 构建可复现，不依赖本地 Inno 安装的语言包）
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Files]
Source: "..\src\QuotaMonitor\bin\Release\net48\QuotaMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent