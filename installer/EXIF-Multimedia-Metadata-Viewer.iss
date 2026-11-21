#define AppName "EXIF查看器"
#define AppExeName GetEnv("EXIF_APP_EXE_NAME")
#define AppVersion GetEnv("EXIF_INSTALLER_VERSION")
#define PublishDir GetEnv("EXIF_PUBLISH_DIR")
#define InstallerOutputDir GetEnv("EXIF_INSTALLER_OUTPUT_DIR")
#define InstallerBaseName GetEnv("EXIF_INSTALLER_BASE_NAME")

[Setup]
AppId={{6AD6DD3D-9E20-4693-9B36-DC1200201ABA}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=FurryFGG
AppPublisherURL=https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer
DefaultDirName={localappdata}\Programs\EXIF查看器\WinUI-2.0.1
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName} v{#AppVersion}
PrivilegesRequired=lowest
MinVersion=10.0.19045
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#InstallerOutputDir}
OutputBaseFilename={#InstallerBaseName}
UninstallDisplayIcon={app}\{#AppExeName}
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName} v{#AppVersion}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName} v{#AppVersion}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: postinstall nowait skipifsilent
