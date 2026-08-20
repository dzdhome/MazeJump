; Inno Setup 脚本 - JumpGameMonoGame (Windows 64位 安装包)
; 编译前请先执行发布命令生成 publish\win-x64 目录：
;   dotnet publish JumpGameMonoGame.csproj -c Release -r win-x64 --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64

#define MyAppName "JumpGameMonoGame"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "JumpGameMonoGame"
#define MyAppExeName "JumpGameMonoGame.exe"
#define MyPublishDir "..\publish\win-x64"

[Setup]
AppId={{B6C1E9C4-2E9A-4A5B-9C1D-1F2E4A6B7C8D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 仅支持 64 位 Windows
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=JumpGameMonoGame_Setup_win64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableDirPage=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
