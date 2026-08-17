[Setup]
AppName=DeepSeek Balance Monitor
AppVersion=1.1.0
AppPublisher=DeepSeekBalanceMonitor
DefaultDirName={autopf}\DeepSeekBalanceMonitor
DefaultGroupName=DeepSeek Balance Monitor
OutputDir=installer
OutputBaseFilename=DeepSeekBalanceMonitor-Setup-v1.1.0
Compression=lzma2
SolidCompression=yes
SetupIconFile=src\DeepSeekBalanceMonitor\app.ico
UninstallDisplayIcon={app}\DeepSeekBalanceMonitor.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"

[Files]
Source: "publish\Release\DeepSeekBalanceMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\Release\DeepSeekBalanceMonitor.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DeepSeek Balance Monitor"; Filename: "{app}\DeepSeekBalanceMonitor.exe"
Name: "{autodesktop}\DeepSeek Balance Monitor"; Filename: "{app}\DeepSeekBalanceMonitor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DeepSeekBalanceMonitor.exe"; Description: "启动程序"; Flags: nowait postinstall skipifsilent
