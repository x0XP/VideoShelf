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
WizardSizePercent=115
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

  { VideoShelf / Xdolf palette. TColor uses BGR byte order. }
  VSBackground = $00140D06;  { #060D14 }
  VSSidebar    = $00171007;  { #071017 }
  VSPanel      = $001A1209;  { #09121A }
  VSRaised     = $0024190D;  { #0D1924 }
  VSOutline    = $00463623;  { #233646 }
  VSBlue       = $00FF941F;  { #1F94FF }
  VSRed        = $00423AFF;  { #FF3A42 }
  VSText       = $00ECE2D7;  { #D7E2EC }
  VSStrong     = $00FCF9F6;  { #F6F9FC }
  VSMuted      = $00BEAB99;  { #99ABBE }

var
  HeaderBlue: TPanel;
  HeaderRed: TPanel;
  FooterLine: TPanel;
  WelcomeCard: TPanel;
  WelcomeLogo: TPanel;
  WelcomeTitle: TNewStaticText;
  WelcomeVersion: TNewStaticText;
  FinishCard: TPanel;
  FinishLogo: TPanel;
  FinishTitle: TNewStaticText;
  FinishVersion: TNewStaticText;

function SetWindowTheme(hwnd: HWND; pszSubAppName, pszSubIdList: String): Integer;
  external 'SetWindowTheme@uxtheme.dll stdcall';

procedure ThemeButton(Button: TNewButton);
begin
  Button.Font.Name := 'Segoe UI Semibold';
  Button.Font.Size := 9;
  Button.Font.Color := VSText;
  SetWindowTheme(Button.Handle, 'DarkMode_Explorer', '');
end;

procedure ThemeEdit(Edit: TNewEdit);
begin
  Edit.Color := VSPanel;
  Edit.Font.Name := 'Segoe UI';
  Edit.Font.Size := 9;
  Edit.Font.Color := VSStrong;
  SetWindowTheme(Edit.Handle, 'DarkMode_Explorer', '');
end;

procedure ThemeMemo(Memo: TNewMemo);
begin
  Memo.Color := VSPanel;
  Memo.Font.Name := 'Segoe UI';
  Memo.Font.Size := 9;
  Memo.Font.Color := VSText;
  SetWindowTheme(Memo.Handle, 'DarkMode_Explorer', '');
end;

procedure ThemeChecklist(List: TNewCheckListBox);
begin
  List.Color := VSPanel;
  List.Font.Name := 'Segoe UI';
  List.Font.Size := 9;
  List.Font.Color := VSText;
  SetWindowTheme(List.Handle, 'DarkMode_Explorer', '');
end;

procedure CreateLogoMark(Parent: TWinControl; X, Y, Size: Integer; var Holder: TPanel);
var
  TopLine: TPanel;
  LeftLine: TPanel;
  Bar1: TPanel;
  Bar2: TPanel;
  Bar3: TPanel;
  Bar4: TPanel;
  UnitW: Integer;
begin
  Holder := TPanel.Create(WizardForm);
  Holder.Parent := Parent;
  Holder.SetBounds(X, Y, Size, Size);
  Holder.BevelOuter := bvNone;
  Holder.Color := VSRaised;

  TopLine := TPanel.Create(WizardForm);
  TopLine.Parent := Holder;
  TopLine.SetBounds(3, 0, Size - 3, ScaleY(2));
  TopLine.BevelOuter := bvNone;
  TopLine.Color := VSBlue;

  LeftLine := TPanel.Create(WizardForm);
  LeftLine.Parent := Holder;
  LeftLine.SetBounds(0, 0, ScaleX(4), Size);
  LeftLine.BevelOuter := bvNone;
  LeftLine.Color := VSRed;

  UnitW := (Size - ScaleX(28)) div 4;

  Bar1 := TPanel.Create(WizardForm);
  Bar1.Parent := Holder;
  Bar1.SetBounds(ScaleX(12), Size - ScaleY(18), UnitW, ScaleY(9));
  Bar1.BevelOuter := bvNone;
  Bar1.Color := $009D7651;

  Bar2 := TPanel.Create(WizardForm);
  Bar2.Parent := Holder;
  Bar2.SetBounds(ScaleX(12) + UnitW + ScaleX(3), Size - ScaleY(28), UnitW, ScaleY(19));
  Bar2.BevelOuter := bvNone;
  Bar2.Color := $009D7651;

  Bar3 := TPanel.Create(WizardForm);
  Bar3.Parent := Holder;
  Bar3.SetBounds(ScaleX(12) + (UnitW + ScaleX(3)) * 2, Size - ScaleY(23), UnitW, ScaleY(14));
  Bar3.BevelOuter := bvNone;
  Bar3.Color := $009D7651;

  Bar4 := TPanel.Create(WizardForm);
  Bar4.Parent := Holder;
  Bar4.SetBounds(ScaleX(12) + (UnitW + ScaleX(3)) * 3, Size - ScaleY(36), UnitW, ScaleY(27));
  Bar4.BevelOuter := bvNone;
  Bar4.Color := VSBlue;
end;

procedure CreateBrandCard(Page: TWinControl; IsFinish: Boolean);
var
  Card: TPanel;
  Logo: TPanel;
  TitleText: TNewStaticText;
  VersionText: TNewStaticText;
  Accent: TPanel;
begin
  Card := TPanel.Create(WizardForm);
  Card.Parent := Page;
  Card.SetBounds(ScaleX(28), ScaleY(34), ScaleX(130), ScaleY(126));
  Card.BevelOuter := bvNone;
  Card.Color := VSRaised;

  Accent := TPanel.Create(WizardForm);
  Accent.Parent := Card;
  Accent.SetBounds(0, 0, ScaleX(4), Card.Height);
  Accent.BevelOuter := bvNone;
  Accent.Color := VSRed;

  CreateLogoMark(Card, ScaleX(18), ScaleY(18), ScaleX(58), Logo);

  TitleText := TNewStaticText.Create(WizardForm);
  TitleText.Parent := Card;
  TitleText.SetBounds(ScaleX(18), ScaleY(83), ScaleX(103), ScaleY(22));
  TitleText.Caption := 'VideoShelf';
  TitleText.Font.Name := 'Segoe UI Semibold';
  TitleText.Font.Size := 12;
  TitleText.Font.Color := VSStrong;
  TitleText.Color := VSRaised;

  VersionText := TNewStaticText.Create(WizardForm);
  VersionText.Parent := Card;
  VersionText.SetBounds(ScaleX(18), ScaleY(105), ScaleX(103), ScaleY(18));
  VersionText.Caption := 'v{#AppVersion}';
  VersionText.Font.Name := 'Segoe UI';
  VersionText.Font.Size := 8;
  VersionText.Font.Color := VSMuted;
  VersionText.Color := VSRaised;

  if IsFinish then
  begin
    FinishCard := Card;
    FinishLogo := Logo;
    FinishTitle := TitleText;
    FinishVersion := VersionText;
  end
  else
  begin
    WelcomeCard := Card;
    WelcomeLogo := Logo;
    WelcomeTitle := TitleText;
    WelcomeVersion := VersionText;
  end;
end;

procedure StyleWizardPages();
begin
  WizardForm.Color := VSBackground;
  WizardForm.MainPanel.Color := VSSidebar;
  WizardForm.InnerPage.Color := VSBackground;
  WizardForm.WelcomePage.Color := VSBackground;
  WizardForm.SelectDirPage.Color := VSBackground;
  WizardForm.SelectTasksPage.Color := VSBackground;
  WizardForm.ReadyPage.Color := VSBackground;
  WizardForm.PreparingPage.Color := VSBackground;
  WizardForm.InstallingPage.Color := VSBackground;
  WizardForm.FinishedPage.Color := VSBackground;

  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardSmallBitmapImage.Visible := False;

  WizardForm.PageNameLabel.Font.Name := 'Segoe UI Semibold';
  WizardForm.PageNameLabel.Font.Size := 14;
  WizardForm.PageNameLabel.Font.Color := VSStrong;
  WizardForm.PageDescriptionLabel.Font.Name := 'Segoe UI';
  WizardForm.PageDescriptionLabel.Font.Size := 9;
  WizardForm.PageDescriptionLabel.Font.Color := VSMuted;

  WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI Semibold';
  WizardForm.WelcomeLabel1.Font.Size := 20;
  WizardForm.WelcomeLabel1.Font.Color := VSStrong;
  WizardForm.WelcomeLabel1.Left := ScaleX(188);
  WizardForm.WelcomeLabel1.Top := ScaleY(50);
  WizardForm.WelcomeLabel1.Width := WizardForm.WelcomePage.Width - ScaleX(216);
  WizardForm.WelcomeLabel1.Caption := 'Install VideoShelf';

  WizardForm.WelcomeLabel2.Font.Name := 'Segoe UI';
  WizardForm.WelcomeLabel2.Font.Size := 10;
  WizardForm.WelcomeLabel2.Font.Color := VSMuted;
  WizardForm.WelcomeLabel2.Left := ScaleX(188);
  WizardForm.WelcomeLabel2.Top := ScaleY(104);
  WizardForm.WelcomeLabel2.Width := WizardForm.WelcomePage.Width - ScaleX(216);
  WizardForm.WelcomeLabel2.Height := ScaleY(150);

  WizardForm.FinishedHeadingLabel.Font.Name := 'Segoe UI Semibold';
  WizardForm.FinishedHeadingLabel.Font.Size := 20;
  WizardForm.FinishedHeadingLabel.Font.Color := VSStrong;
  WizardForm.FinishedHeadingLabel.Left := ScaleX(188);
  WizardForm.FinishedHeadingLabel.Top := ScaleY(50);
  WizardForm.FinishedHeadingLabel.Width := WizardForm.FinishedPage.Width - ScaleX(216);

  WizardForm.FinishedLabel.Font.Name := 'Segoe UI';
  WizardForm.FinishedLabel.Font.Size := 10;
  WizardForm.FinishedLabel.Font.Color := VSMuted;
  WizardForm.FinishedLabel.Left := ScaleX(188);
  WizardForm.FinishedLabel.Top := ScaleY(104);
  WizardForm.FinishedLabel.Width := WizardForm.FinishedPage.Width - ScaleX(216);

  WizardForm.SelectDirLabel.Font.Color := VSText;
  WizardForm.SelectDirBrowseLabel.Font.Color := VSMuted;
  WizardForm.SelectTasksLabel.Font.Color := VSText;
  WizardForm.ReadyLabel.Font.Color := VSText;
  WizardForm.PreparingLabel.Font.Color := VSText;
  WizardForm.InstallingLabel.Font.Color := VSText;
  WizardForm.FilenameLabel.Font.Color := VSMuted;
  WizardForm.StatusLabel.Font.Color := VSText;

  ThemeEdit(WizardForm.DirEdit);
  ThemeMemo(WizardForm.ReadyMemo);
  ThemeChecklist(WizardForm.TasksList);
  ThemeChecklist(WizardForm.RunList);
  ThemeButton(WizardForm.DirBrowseButton);
  ThemeButton(WizardForm.BackButton);
  ThemeButton(WizardForm.NextButton);
  ThemeButton(WizardForm.CancelButton);

  WizardForm.ProgressGauge.ForeColor := VSBlue;
  WizardForm.ProgressGauge.BackColor := VSPanel;
  SetWindowTheme(WizardForm.ProgressGauge.Handle, 'DarkMode_Explorer', '');
end;

procedure AddChrome();
begin
  HeaderBlue := TPanel.Create(WizardForm);
  HeaderBlue.Parent := WizardForm.MainPanel;
  HeaderBlue.SetBounds(ScaleX(4), 0, WizardForm.MainPanel.Width - ScaleX(4), ScaleY(2));
  HeaderBlue.Anchors := [akLeft, akTop, akRight];
  HeaderBlue.BevelOuter := bvNone;
  HeaderBlue.Color := VSBlue;

  HeaderRed := TPanel.Create(WizardForm);
  HeaderRed.Parent := WizardForm.MainPanel;
  HeaderRed.SetBounds(0, 0, ScaleX(4), WizardForm.MainPanel.Height);
  HeaderRed.Anchors := [akLeft, akTop, akBottom];
  HeaderRed.BevelOuter := bvNone;
  HeaderRed.Color := VSRed;

  FooterLine := TPanel.Create(WizardForm);
  FooterLine.Parent := WizardForm;
  FooterLine.SetBounds(0, WizardForm.NextButton.Top - ScaleY(14), WizardForm.ClientWidth, ScaleY(1));
  FooterLine.Anchors := [akLeft, akRight, akBottom];
  FooterLine.BevelOuter := bvNone;
  FooterLine.Color := VSOutline;

  CreateBrandCard(WizardForm.WelcomePage, False);
  CreateBrandCard(WizardForm.FinishedPage, True);
end;

procedure InitializeWizard();
begin
  StyleWizardPages();
  AddChrome();
  WizardForm.Caption := 'VideoShelf Setup';
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.Font.Size := 9;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { Keep native navigation semantics while reapplying the dark Windows theme. }
  ThemeButton(WizardForm.BackButton);
  ThemeButton(WizardForm.NextButton);
  ThemeButton(WizardForm.CancelButton);

  if CurPageID = wpReady then
    WizardForm.ReadyMemo.Color := VSPanel;
  if CurPageID = wpInstalling then
    WizardForm.ProgressGauge.ForeColor := VSBlue;
end;

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
