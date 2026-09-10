#ifndef SourceDir
  #define SourceDir "..\VideoShelf-package"
#endif
#ifndef OutputDir
  #define OutputDir ".\output"
#endif
#ifndef AppVersion
  #define AppVersion "1.7.0"
#endif

#define AppName "VideoShelf"
#define AppExeName "VideoShelf.exe"
#define Publisher "x0XP"
#define ProjectUrl "https://github.com/x0XP/VideoShelf"

[Setup]
AppId={{52B6117F-2222-49BB-935D-8C7FA18DC42B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL={#ProjectUrl}
AppSupportURL={#ProjectUrl}
AppUpdatesURL={#ProjectUrl}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=VideoShelf-Setup-v1.7
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
VersionInfoVersion=1.7.0.0
VersionInfoCompany={#Publisher}
VersionInfoDescription={#AppName} Windows Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\VideoShelf.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\TransferHost\*"; DestDir: "{app}\TransferHost"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VideoShelf"; Filename: "{app}\VideoShelf.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\VideoShelf"; Filename: "{app}\VideoShelf.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoShelf.exe"; Description: "Launch VideoShelf"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNet48Release = 528040;

function HasDotNet48(): Boolean;
var
  ReleaseValue: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) then
    Result := ReleaseValue >= DotNet48Release;
  if (not Result) and RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) then
    Result := ReleaseValue >= DotNet48Release;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not HasDotNet48() then
  begin
    MsgBox('VideoShelf requires Microsoft .NET Framework 4.8. Install .NET Framework 4.8, then run this installer again.', mbError, MB_OK);
    Result := False;
  end;
end;
