[Setup]
AppName=QuotaMonitor
AppVersion=1.1.0
AppPublisher=QuotaMonitor
DefaultDirName={autopf}\QuotaMonitor
DefaultGroupName=QuotaMonitor
OutputDir=installer
OutputBaseFilename=QuotaMonitor-Setup-v1.1.0
Compression=lzma2
SolidCompression=yes
SetupIconFile=src\QuotaMonitor\app.ico
UninstallDisplayIcon={app}\QuotaMonitor.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"

[Files]
Source: "publish\Release\QuotaMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\Release\QuotaMonitor.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\QuotaMonitor"; Filename: "{app}\QuotaMonitor.exe"
Name: "{autodesktop}\QuotaMonitor"; Filename: "{app}\QuotaMonitor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\QuotaMonitor.exe"; Description: "启动程序"; Flags: nowait postinstall skipifsilent