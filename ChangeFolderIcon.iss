; -- Inno Setup Script --
[Setup]
; 定义应用程序的基本信息
AppName=ChangeFolderIcon APP
AppVersion=1.0.4
AppPublisher=IDLE_CLOUD
AppPublisherURL=https://github.com/YILING0013
; 安装路径, {commonpf} 表示 Program Files/Program Files (x86) 文件夹
DefaultDirName={autopf}\ChangeFolderIcon
; 开始菜单组名称
DefaultGroupName=ChangeFolderIcon 
; 输出的安装包名称
OutputBaseFilename=ChangeFolderIcon_Setup 
; 使用 lzma 压缩算法
Compression=lzma 
; 使用固体压缩
SolidCompression=yes 
; 不创建程序组
DisableProgramGroupPage=yes 
; 设置安装程序的图标
SetupIconFile=E:\Visual_Studio_project\ChangeFolderIcon\ChangeFolderIcon\Assets\icon\app_icon.ico
; 使用现代风格向导
WizardStyle=modern

; 卸载设置
UninstallDisplayIcon={app}\app_icon.ico
UninstallDisplayName=卸载 ChangeFolderIcon

[Tasks]
; 自动处理桌面图标的创建
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked


[Files]
#define SourcePath "E:\Visual_Studio_project\ChangeFolderIcon\ChangeFolderIcon\bin\unpackage\publish\arm64"
; 将应用程序的文件添加到安装程序中
Source: "{#SourcePath}\ChangeFolderIcon.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 将图标文件添加到安装程序中
Source: "E:\Visual_Studio_project\ChangeFolderIcon\ChangeFolderIcon\Assets\icon\app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; 创建桌面和开始菜单快捷方式，并设置快捷方式图标
Name: "{group}\ChangeFolderIcon"; Filename: "{app}\ChangeFolderIcon.exe"; IconFilename: "{app}\app_icon.ico"
Name: "{commondesktop}\ChangeFolderIcon"; Filename: "{app}\ChangeFolderIcon.exe"; Tasks: desktopicon; IconFilename: "{app}\app_icon.ico"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\ChangeFolderIcon"; Filename: "{app}\ChangeFolderIcon.exe"; Tasks: quicklaunchicon; IconFilename: "{app}\app_icon.ico"

[Run]
; 安装后自动运行应用程序
Filename: "{app}\ChangeFolderIcon.exe"; Description: "{cm:LaunchProgram,ChangeFolderIcon}"; Flags: nowait postinstall skipifsilent unchecked
