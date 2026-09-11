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
  WelcomeSidePanel: TPanel;
  FinishSidePanel: TPanel;
  WelcomeCard: TPanel;
  WelcomeLogo: TBitmapImage;
  WelcomeTitle: TNewStaticText;
  WelcomeVersion: TNewStaticText;
  FinishCard: TPanel;
  FinishLogo: TBitmapImage;
  FinishTitle: TNewStaticText;
  FinishVersion: TNewStaticText;
  ReadySummary: TNewMemo;
  ProgressTrack: TPanel;
  ProgressFill: TPanel;

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

procedure CreateBrandCard(Parent: TWinControl; IsFinish: Boolean);
var
  Card: TPanel;
  Logo: TBitmapImage;
  TitleText: TNewStaticText;
  VersionText: TNewStaticText;
  Accent: TPanel;
  TopAccent: TPanel;
begin
  Card := TPanel.Create(WizardForm);
  Card.Parent := Parent;
  Card.SetBounds(ScaleX(20), ScaleY(44), ScaleX(138), ScaleY(145));
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

procedure SuppressDefaultArtwork();
begin
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardSmallBitmapImage.Visible := False;
end;

procedure ApplyWelcomeLayout();
var
  SideWidth: Integer;
  ContentLeft: Integer;
  ContentWidth: Integer;
begin
  SuppressDefaultArtwork();
  WizardForm.WelcomePage.Color := VSBackground;

  SideWidth := ScaleX(190);
  WelcomeSidePanel.SetBounds(0, 0, SideWidth, WizardForm.WelcomePage.Height);
  WelcomeCard.Left := (WelcomeSidePanel.Width - WelcomeCard.Width) div 2;
  WelcomeCard.Top := ScaleY(44);

  ContentLeft := SideWidth + ScaleX(34);
  ContentWidth := WizardForm.WelcomePage.Width - ContentLeft - ScaleX(36);

  WizardForm.WelcomeLabel1.AutoSize := False;
  WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI Semibold';
  WizardForm.WelcomeLabel1.Font.Size := 20;
  WizardForm.WelcomeLabel1.Font.Color := VSStrong;
  WizardForm.WelcomeLabel1.SetBounds(ContentLeft, ScaleY(52), ContentWidth, ScaleY(42));
  WizardForm.WelcomeLabel1.Caption := 'Install VideoShelf';

  WizardForm.WelcomeLabel2.AutoSize := False;
  WizardForm.WelcomeLabel2.Font.Name := 'Segoe UI';
  WizardForm.WelcomeLabel2.Font.Size := 10;
  WizardForm.WelcomeLabel2.Font.Color := VSMuted;
  WizardForm.WelcomeLabel2.SetBounds(ContentLeft, ScaleY(112), ContentWidth, ScaleY(160));

  WelcomeSidePanel.BringToFront;
  WelcomeCard.BringToFront;
  WizardForm.WelcomeLabel1.BringToFront;
  WizardForm.WelcomeLabel2.BringToFront;
end;

procedure ApplyFinishLayout();
var
  SideWidth: Integer;
  ContentLeft: Integer;
  ContentWidth: Integer;
begin
  SuppressDefaultArtwork();
  WizardForm.FinishedPage.Color := VSBackground;

  SideWidth := ScaleX(190);
  FinishSidePanel.SetBounds(0, 0, SideWidth, WizardForm.FinishedPage.Height);
  FinishCard.Left := (FinishSidePanel.Width - FinishCard.Width) div 2;
  FinishCard.Top := ScaleY(44);

  ContentLeft := SideWidth + ScaleX(34);
  ContentWidth := WizardForm.FinishedPage.Width - ContentLeft - ScaleX(36);

  WizardForm.FinishedHeadingLabel.AutoSize := False;
  WizardForm.FinishedHeadingLabel.Font.Name := 'Segoe UI Semibold';
  WizardForm.FinishedHeadingLabel.Font.Size := 18;
  WizardForm.FinishedHeadingLabel.Font.Color := VSStrong;
  WizardForm.FinishedHeadingLabel.SetBounds(ContentLeft, ScaleY(52), ContentWidth, ScaleY(42));
  WizardForm.FinishedHeadingLabel.Caption := 'VideoShelf installed';

  WizardForm.FinishedLabel.AutoSize := False;
  WizardForm.FinishedLabel.Font.Name := 'Segoe UI';
  WizardForm.FinishedLabel.Font.Size := 10;
  WizardForm.FinishedLabel.Font.Color := VSMuted;
  WizardForm.FinishedLabel.SetBounds(ContentLeft, ScaleY(112), ContentWidth, ScaleY(86));
  WizardForm.FinishedLabel.Caption := 'Setup has finished installing VideoShelf on your computer.' + #13#10 + #13#10 + 'Click Finish to exit Setup.';

  WizardForm.RunList.SetBounds(ContentLeft, ScaleY(218), ContentWidth, ScaleY(54));
  ThemeChecklist(WizardForm.RunList);

  FinishSidePanel.BringToFront;
  FinishCard.BringToFront;
  WizardForm.FinishedHeadingLabel.BringToFront;
  WizardForm.FinishedLabel.BringToFront;
  WizardForm.RunList.BringToFront;
end;

procedure ApplyStandardPageLayout();
var
  LeftMargin: Integer;
  RightMargin: Integer;
  TasksWidth: Integer;
  ReadyWidth: Integer;
  InstallWidth: Integer;
  TrackHeight: Integer;
  TrackTop: Integer;
begin
  LeftMargin := ScaleX(48);
  RightMargin := ScaleX(36);

  TasksWidth := WizardForm.SelectTasksPage.Width - LeftMargin - RightMargin;
  WizardForm.SelectTasksLabel.Left := LeftMargin;
  WizardForm.SelectTasksLabel.Width := TasksWidth;
  WizardForm.TasksList.SetBounds(LeftMargin, WizardForm.TasksList.Top, TasksWidth, ScaleY(138));

  ReadyWidth := WizardForm.ReadyPage.Width - LeftMargin - RightMargin;
  WizardForm.ReadyLabel.Left := LeftMargin;
  WizardForm.ReadyLabel.Width := ReadyWidth;
  ReadySummary.SetBounds(LeftMargin, WizardForm.ReadyMemo.Top, ReadyWidth, ScaleY(150));

  InstallWidth := WizardForm.InstallingPage.Width - LeftMargin - RightMargin;
  WizardForm.StatusLabel.Left := LeftMargin;
  WizardForm.StatusLabel.Width := InstallWidth;
  WizardForm.FilenameLabel.Left := LeftMargin;
  WizardForm.FilenameLabel.Width := InstallWidth;

  TrackHeight := ScaleY(12);
  TrackTop := WizardForm.ProgressGauge.Top + ((WizardForm.ProgressGauge.Height - TrackHeight) div 2);
  ProgressTrack.SetBounds(LeftMargin, TrackTop, InstallWidth, TrackHeight);
  ProgressFill.Height := TrackHeight;
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

  SuppressDefaultArtwork();

  WizardForm.PageNameLabel.Font.Name := 'Segoe UI Semibold';
  WizardForm.PageNameLabel.Font.Size := 14;
  WizardForm.PageNameLabel.Font.Color := VSStrong;
  WizardForm.PageDescriptionLabel.Font.Name := 'Segoe UI';
  WizardForm.PageDescriptionLabel.Font.Size := 9;
  WizardForm.PageDescriptionLabel.Font.Color := VSMuted;

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

  WelcomeSidePanel := TPanel.Create(WizardForm);
  WelcomeSidePanel.Parent := WizardForm.WelcomePage;
  WelcomeSidePanel.SetBounds(0, 0, ScaleX(190), WizardForm.WelcomePage.Height);
  WelcomeSidePanel.Anchors := [akLeft, akTop, akBottom];
  WelcomeSidePanel.BevelOuter := bvNone;
  WelcomeSidePanel.Color := VSSidebar;
  CreateBrandCard(WelcomeSidePanel, False);

  FinishSidePanel := TPanel.Create(WizardForm);
  FinishSidePanel.Parent := WizardForm.FinishedPage;
  FinishSidePanel.SetBounds(0, 0, ScaleX(190), WizardForm.FinishedPage.Height);
  FinishSidePanel.Anchors := [akLeft, akTop, akBottom];
  FinishSidePanel.BevelOuter := bvNone;
  FinishSidePanel.Color := VSSidebar;
  CreateBrandCard(FinishSidePanel, True);

  ReadySummary := TNewMemo.Create(WizardForm);
  ReadySummary.Parent := WizardForm.ReadyPage;
  ReadySummary.ReadOnly := True;
  ReadySummary.TabStop := False;
  ReadySummary.Visible := False;
  ThemeMemo(ReadySummary);

  ProgressTrack := TPanel.Create(WizardForm);
  ProgressTrack.Parent := WizardForm.InstallingPage;
  ProgressTrack.BevelOuter := bvNone;
  ProgressTrack.Color := VSOutline;
  ProgressTrack.Visible := False;

  ProgressFill := TPanel.Create(WizardForm);
  ProgressFill.Parent := ProgressTrack;
  ProgressFill.SetBounds(0, 0, 0, ScaleY(12));
  ProgressFill.BevelOuter := bvNone;
  ProgressFill.Color := VSBlue;
end;

procedure InitializeWizard();
begin
  ExtractTemporaryFile('VideoShelfInstallerLogo.bmp');
  StyleWizardPages();
  AddChrome();
  ApplyStandardPageLayout();
  ApplyWelcomeLayout();
  ApplyFinishLayout();
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
  SuppressDefaultArtwork();
  ApplyStandardPageLayout();

  ReadySummary.Visible := False;
  ProgressTrack.Visible := False;

  if CurPageID = wpReady then
  begin
    WizardForm.ReadyMemo.Visible := False;
    ReadySummary.Text := WizardForm.ReadyMemo.Text;
    ReadySummary.Visible := True;
    ReadySummary.BringToFront;
  end
  else
    WizardForm.ReadyMemo.Visible := True;

  if CurPageID = wpInstalling then
  begin
    WizardForm.ProgressGauge.Visible := False;
    ProgressFill.SetBounds(0, 0, 0, ProgressTrack.Height);
    ProgressTrack.Visible := True;
    ProgressTrack.BringToFront;
  end
  else
    WizardForm.ProgressGauge.Visible := True;

  if CurPageID = wpWelcome then
    ApplyWelcomeLayout();

  if CurPageID = wpFinished then
    ApplyFinishLayout();
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
var
  NewWidth: Integer;
begin
  if (ProgressTrack = nil) or (ProgressFill = nil) or (MaxProgress <= 0) then
    exit;

  NewWidth := (ProgressTrack.Width * CurProgress) div MaxProgress;
  if (NewWidth < ScaleX(2)) and (CurProgress > 0) then
    NewWidth := ScaleX(2);
  if NewWidth > ProgressTrack.Width then
    NewWidth := ProgressTrack.Width;
  ProgressFill.Width := NewWidth;
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
