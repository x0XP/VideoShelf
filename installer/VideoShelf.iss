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
SetupIconFile={#SourceDir}\VideoShelf.ico
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
Source: "{#SourceDir}\VideoShelf.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\TransferHost\*"; DestDir: "{app}\TransferHost"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\VideoShelfInstallerLogo.bmp"; Flags: dontcopy

[Icons]
Name: "{group}\VideoShelf"; Filename: "{app}\VideoShelf.exe"; WorkingDir: "{app}"; IconFilename: "{app}\VideoShelf.ico"
Name: "{autodesktop}\VideoShelf"; Filename: "{app}\VideoShelf.exe"; WorkingDir: "{app}"; IconFilename: "{app}\VideoShelf.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoShelf.exe"; Description: "Launch VideoShelf"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNet48Release = 528040;
  VSBackground = $00140D06;
  VSSidebar    = $00171007;
  VSPanel      = $001A1209;
  VSRaised     = $0024190D;
  VSOutline    = $00463623;
  VSBlue       = $00FF941F;
  VSRed        = $00423AFF;
  VSText       = $00ECE2D7;
  VSStrong     = $00FCF9F6;
  VSMuted      = $00BEAB99;

var
  HeaderBlue: TPanel;
  HeaderRed: TPanel;
  FooterLine: TPanel;
  WelcomeCard: TPanel;
  WelcomeLogo: TBitmapImage;
  WelcomeTitle: TNewStaticText;
  WelcomeVersion: TNewStaticText;
  FinishCard: TPanel;
  FinishLogo: TBitmapImage;
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
  Memo.BorderStyle := bsNone;
  Memo.ScrollBars := ssNone;
  Memo.WordWrap := True;
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

procedure CreateBrandImage(Parent: TWinControl; X, Y, Size: Integer; var Image: TBitmapImage);
begin
  Image := TBitmapImage.Create(WizardForm);
  Image.Parent := Parent;
  Image.SetBounds(X, Y, Size, Size);
  Image.Stretch := True;
  Image.Bitmap.LoadFromFile(ExpandConstant('{tmp}\VideoShelfInstallerLogo.bmp'));
end;

procedure CreateBrandCard(Page: TWinControl; IsFinish: Boolean);
var
  Card: TPanel;
  Logo: TBitmapImage;
  TitleText: TNewStaticText;
  VersionText: TNewStaticText;
  Accent: TPanel;
  TopAccent: TPanel;
begin
  Card := TPanel.Create(WizardForm);
  Card.Parent := Page;
  Card.SetBounds(ScaleX(28), ScaleY(30), ScaleX(138), ScaleY(145));
  Card.BevelOuter := bvNone;
  Card.Color := VSRaised;

  Accent := TPanel.Create(WizardForm);
  Accent.Parent := Card;
  Accent.SetBounds(0, 0, ScaleX(4), Card.Height);
  Accent.BevelOuter := bvNone;
  Accent.Color := VSRed;

  TopAccent := TPanel.Create(WizardForm);
  TopAccent.Parent := Card;
  TopAccent.SetBounds(ScaleX(4), 0, Card.Width - ScaleX(4), ScaleY(2));
  TopAccent.BevelOuter := bvNone;
  TopAccent.Color := VSBlue;

  CreateBrandImage(Card, ScaleX(25), ScaleY(14), ScaleX(88), Logo);

  TitleText := TNewStaticText.Create(WizardForm);
  TitleText.Parent := Card;
  TitleText.SetBounds(ScaleX(18), ScaleY(105), ScaleX(112), ScaleY(22));
  TitleText.Caption := 'VideoShelf';
  TitleText.Font.Name := 'Segoe UI Semibold';
  TitleText.Font.Size := 12;
  TitleText.Font.Color := VSStrong;
  TitleText.Color := VSRaised;

  VersionText := TNewStaticText.Create(WizardForm);
  VersionText.Parent := Card;
  VersionText.SetBounds(ScaleX(18), ScaleY(126), ScaleX(112), ScaleY(16));
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
  WizardForm.WelcomeLabel1.Left := ScaleX(194);
  WizardForm.WelcomeLabel1.Top := ScaleY(50);
  WizardForm.WelcomeLabel1.Width := WizardForm.WelcomePage.Width - ScaleX(222);
  WizardForm.WelcomeLabel1.Caption := 'Install VideoShelf';

  WizardForm.WelcomeLabel2.Font.Name := 'Segoe UI';
  WizardForm.WelcomeLabel2.Font.Size := 10;
  WizardForm.WelcomeLabel2.Font.Color := VSMuted;
  WizardForm.WelcomeLabel2.Left := ScaleX(194);
  WizardForm.WelcomeLabel2.Top := ScaleY(104);
  WizardForm.WelcomeLabel2.Width := WizardForm.WelcomePage.Width - ScaleX(222);
  WizardForm.WelcomeLabel2.Height := ScaleY(150);

  WizardForm.FinishedHeadingLabel.Font.Name := 'Segoe UI Semibold';
  WizardForm.FinishedHeadingLabel.Font.Size := 18;
  WizardForm.FinishedHeadingLabel.Font.Color := VSStrong;
  WizardForm.FinishedHeadingLabel.Left := ScaleX(194);
  WizardForm.FinishedHeadingLabel.Top := ScaleY(50);
  WizardForm.FinishedHeadingLabel.Width := WizardForm.FinishedPage.Width - ScaleX(222);
  WizardForm.FinishedHeadingLabel.Height := ScaleY(40);
  WizardForm.FinishedHeadingLabel.Caption := 'VideoShelf installed';

  WizardForm.FinishedLabel.Font.Name := 'Segoe UI';
  WizardForm.FinishedLabel.Font.Size := 10;
  WizardForm.FinishedLabel.Font.Color := VSMuted;
  WizardForm.FinishedLabel.Left := ScaleX(194);
  WizardForm.FinishedLabel.Top := ScaleY(104);
  WizardForm.FinishedLabel.Width := WizardForm.FinishedPage.Width - ScaleX(222);
  WizardForm.FinishedLabel.Height := ScaleY(120);

  WizardForm.SelectDirLabel.Font.Color := VSText;
  WizardForm.SelectDirBrowseLabel.Font.Color := VSMuted;
  WizardForm.DiskSpaceLabel.Font.Name := 'Segoe UI';
  WizardForm.DiskSpaceLabel.Font.Size := 9;
  WizardForm.DiskSpaceLabel.Font.Color := VSMuted;
  WizardForm.SelectTasksLabel.Font.Color := VSText;
  WizardForm.ReadyLabel.Font.Color := VSText;
  WizardForm.PreparingLabel.Font.Color := VSText;
  WizardForm.FilenameLabel.Font.Color := VSMuted;
  WizardForm.StatusLabel.Font.Color := VSText;

  WizardForm.SelectDirBitmapImage.Bitmap.LoadFromFile(ExpandConstant('{tmp}\VideoShelfInstallerLogo.bmp'));
  WizardForm.SelectDirBitmapImage.Stretch := True;
  WizardForm.SelectDirBitmapImage.AutoSize := False;
  WizardForm.SelectDirBitmapImage.SetBounds(WizardForm.SelectDirBitmapImage.Left, WizardForm.SelectDirBitmapImage.Top, ScaleX(48), ScaleY(48));

  ThemeEdit(WizardForm.DirEdit);
  ThemeMemo(WizardForm.ReadyMemo);
  ThemeChecklist(WizardForm.TasksList);
  ThemeChecklist(WizardForm.RunList);
  ThemeButton(WizardForm.DirBrowseButton);
  ThemeButton(WizardForm.BackButton);
  ThemeButton(WizardForm.NextButton);
  ThemeButton(WizardForm.CancelButton);
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
  ExtractTemporaryFile('VideoShelfInstallerLogo.bmp');
  StyleWizardPages();
  AddChrome();
  WizardForm.Caption := 'VideoShelf Setup';
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.Font.Size := 9;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ThemeButton(WizardForm.BackButton);
  ThemeButton(WizardForm.NextButton);
  ThemeButton(WizardForm.CancelButton);
  WizardForm.DiskSpaceLabel.Font.Color := VSMuted;

  { Inno Setup can restore the large wizard bitmap when switching to the
    welcome/finished pages. Keep the default artwork suppressed so it never
    appears behind VideoShelf's own branding. }
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardSmallBitmapImage.Visible := False;

  if CurPageID = wpReady then
  begin
    WizardForm.ReadyMemo.Color := VSPanel;
    WizardForm.ReadyMemo.BorderStyle := bsNone;
    WizardForm.ReadyMemo.ScrollBars := ssNone;
    WizardForm.ReadyMemo.WordWrap := True;
  end;

  if CurPageID = wpWelcome then
  begin
    WelcomeCard.BringToFront;
    WizardForm.WelcomeLabel1.BringToFront;
    WizardForm.WelcomeLabel2.BringToFront;
  end;

  if CurPageID = wpFinished then
  begin
    WizardForm.FinishedPage.Color := VSBackground;
    FinishCard.BringToFront;
    WizardForm.FinishedHeadingLabel.BringToFront;
    WizardForm.FinishedLabel.BringToFront;
    WizardForm.RunList.BringToFront;
  end;
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
