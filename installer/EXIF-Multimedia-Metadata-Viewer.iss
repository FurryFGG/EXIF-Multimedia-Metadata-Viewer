#define AppName "MetaLens 2"
#define AppExeName GetEnv("EXIF_APP_EXE_NAME")
#define AppVersion GetEnv("EXIF_INSTALLER_VERSION")
#define PublishDir GetEnv("EXIF_PUBLISH_DIR")
#define InstallerOutputDir GetEnv("EXIF_INSTALLER_OUTPUT_DIR")
#define InstallerBaseName GetEnv("EXIF_INSTALLER_BASE_NAME")

[Setup]
AppId={{32AF6637-846F-40AC-ABAF-103524876ED8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=FurryFGG
AppPublisherURL=https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer
DefaultDirName={localappdata}\Programs\MetaLens 2
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#InstallerOutputDir}
OutputBaseFilename={#InstallerBaseName}
UninstallDisplayIcon={app}\{#AppExeName}
LicenseFile=..\LICENSE
Compression=lzma2/max
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
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: postinstall nowait skipifsilent
