#define AppName "EXIF Multimedia Metadata Viewer"
#define AppExeName "EXIF Multimedia Metadata Viewer.exe"
#define AppVersion GetEnv("EXIF_INSTALLER_VERSION")
#define PublishDir GetEnv("EXIF_PUBLISH_DIR")
#define InstallerOutputDir GetEnv("EXIF_INSTALLER_OUTPUT_DIR")

[Setup]
AppId={{88AB7794-7E8B-40E7-8BD2-A0FAF16DA0D3}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=FurryFGG
AppPublisherURL=https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer
DefaultDirName={localappdata}\Programs\EXIF Multimedia Metadata Viewer
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#InstallerOutputDir}
OutputBaseFilename=EXIF-Multimedia-Metadata-Viewer-Setup
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
Source: "..\third_party\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: postinstall nowait skipifsilent
