using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 readonly Panel collectionsBrowserView=new Panel();
 Label dashboardLibrary,dashboardCollections,dashboardDownloads,dashboardStreams,dashboardOnline,dashboardHint,settingsOnlineHint;
 Button dashboardCollectionsButton,dashboardSearchButton,dashboardAddButton,dashboardLibraryButton;
 Panel statCollections,statDownloads,statStreams,statOnline,dashboardQuickPanel,dashboardInfoPanel;
 Label dashboardQuickTitle,dashboardInfoTitle;
 bool homeDashboardReady;

 void EnsureHomeDashboard(){
  if(homeDashboardReady||IsDisposed)return;
  homeDashboardReady=true;

  collectionsBrowserView.Dock=DockStyle.Fill;
  collectionsBrowserView.BackColor=XdolfTheme.Background;
  collectionsBrowserView.Visible=false;
  mainHost.Controls.Add(collectionsBrowserView);

  Control[] oldHome=homeView.Controls.Cast<Control>().ToArray();
  foreach(Control control in oldHome){homeView.Controls.Remove(control);collectionsBrowserView.Controls.Add(control);}
  homeHeading.Text="Collections";

  BuildDashboardHome();
  BuildSettingsSearchHint();

  navHome.Click+=delegate{ShowDashboardHome();};
  navCollections.Click+=delegate{ShowCollectionsBrowser();};
  navSettings.Click+=delegate{RefreshSettingsSearchHint();};
  back.Click+=delegate{ShowCollectionsBrowser();};
  homeView.VisibleChanged+=delegate{if(homeView.Visible&&section==ShellSection.Collections)BeginInvoke((MethodInvoker)ShowCollectionsBrowser);};
  homeView.Resize+=delegate{if(homeDashboardReady)LayoutDashboardHome();};
  statusRight.TextChanged+=delegate{if(section==ShellSection.Home)RefreshDashboardHome();};
  statusLeft.TextChanged+=delegate{if(section==ShellSection.Home)RefreshDashboardHome();};

  LayoutDashboardHome();RefreshDashboardHome();RefreshSettingsSearchHint();
 }

 void BuildDashboardHome(){
  var heading=new Label{Text="Home",Name="DashboardHeading",Left=30,Top=26,Width=500,Height=40,ForeColor=Color.White,Font=new Font("Segoe UI",23,FontStyle.Bold)};
  dashboardLibrary=new Label{Left=31,Top=69,Width=900,Height=23,ForeColor=XdolfTheme.Muted,AutoEllipsis=true};
  homeView.Controls.AddRange(new Control[]{heading,dashboardLibrary});

  statCollections=MakeDashboardStat("COLLECTIONS",out dashboardCollections,30,122);
  statDownloads=MakeDashboardStat("ACTIVE DOWNLOADS",out dashboardDownloads,250,122);
  statStreams=MakeDashboardStat("ACTIVE STREAMS",out dashboardStreams,470,122);
  statOnline=MakeDashboardStat("ONLINE SEARCH",out dashboardOnline,690,122);
  homeView.Controls.AddRange(new Control[]{statCollections,statDownloads,statStreams,statOnline});

  dashboardQuickTitle=new Label{Text="Quick actions",Left=30,Top=274,Width=300,Height=28,ForeColor=Color.White,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  dashboardQuickPanel=new Panel{Left=30,Top=313,Width=880,Height=126,BackColor=Color.FromArgb(9,18,26)};
  dashboardQuickPanel.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,dashboardQuickPanel.Width-1,dashboardQuickPanel.Height-1);using(var b=new SolidBrush(XdolfTheme.AccentBlue))e.Graphics.FillRectangle(b,0,0,dashboardQuickPanel.Width,2);using(var b=new SolidBrush(XdolfTheme.AccentRed))e.Graphics.FillRectangle(b,0,2,3,dashboardQuickPanel.Height-2);};

  dashboardCollectionsButton=DashboardButton("Browse collections",20,23,190);
  dashboardSearchButton=DashboardButton("Search online",222,23,165);
  dashboardAddButton=DashboardButton("+ Add collection",399,23,175);
  dashboardLibraryButton=DashboardButton("Change library",586,23,160);
  dashboardCollectionsButton.Click+=delegate{ShowCollectionsBrowser();};
  dashboardSearchButton.Click+=delegate{ShowSearch();};
  dashboardAddButton.Click+=delegate{AddLibraryFolder();};
  dashboardLibraryButton.Click+=delegate{choose.PerformClick();};
  dashboardQuickPanel.Controls.AddRange(new Control[]{dashboardCollectionsButton,dashboardSearchButton,dashboardAddButton,dashboardLibraryButton});

  dashboardHint=new Label{Left=20,Top=77,Width=830,Height=28,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",9f),AutoEllipsis=true};
  dashboardQuickPanel.Controls.Add(dashboardHint);
  homeView.Controls.AddRange(new Control[]{dashboardQuickTitle,dashboardQuickPanel});

  dashboardInfoTitle=new Label{Text="Library overview",Left=30,Top=480,Width=300,Height=28,ForeColor=Color.White,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  dashboardInfoPanel=new Panel{Left=30,Top=519,Width=880,Height=118,BackColor=Color.FromArgb(8,17,25)};
  dashboardInfoPanel.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,dashboardInfoPanel.Width-1,dashboardInfoPanel.Height-1);};
  var infoText=new Label{Name="DashboardInfoText",Text="Collections are folders inside your selected VideoShelf library. Open Collections to browse artwork and local videos, or Search to find seeded torrent metadata. Online search never starts a media transfer until you explicitly choose Download or Stream.",Left=20,Top=20,Width=830,Height=72,ForeColor=XdolfTheme.Text,Font=new Font("Segoe UI",9.5f)};
  dashboardInfoPanel.Controls.Add(infoText);homeView.Controls.AddRange(new Control[]{dashboardInfoTitle,dashboardInfoPanel});
 }

 void BuildSettingsSearchHint(){
  settingsSource.Text="Configure custom source";
  settingsOnlineHint=new Label{Left=24,Top=310,Width=760,Height=58,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",9.5f),Text="Built-in public metadata sources are used automatically when no custom source is configured. A custom Torznab/XML/RSS/JSON source is optional."};
  settingsView.Controls.Add(settingsOnlineHint);
 }

 void RefreshSettingsSearchHint(){
  if(settingsOnlineHint==null)return;
  onlineSettings=OnlineSettings.Load();
  settingsOnlineHint.Text=onlineSettings.Configured?"A custom metadata source is configured. Clear its URL to return to VideoShelf's built-in public metadata sources.":"Built-in public metadata sources are active. You can optionally configure a custom Torznab/XML/RSS/JSON metadata source.";
  if(section==ShellSection.Settings)SetStatus("Settings",onlineSettings.Configured?"Custom online metadata source configured":"Built-in online metadata sources active");
 }

 Panel MakeDashboardStat(string title,out Label value,int left,int top){
  var panel=new Panel{Left=left,Top=top,Width=200,Height=112,BackColor=Color.FromArgb(9,18,26)};
  panel.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,panel.Width-1,panel.Height-1);using(var b=new SolidBrush(XdolfTheme.AccentBlue))e.Graphics.FillRectangle(b,0,0,panel.Width,2);};
  var caption=new Label{Text=title,Left=15,Top=15,Width=170,Height=18,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",8f,FontStyle.Bold)};
  value=new Label{Text="—",Left=15,Top=43,Width=170,Height=42,ForeColor=Color.White,Font=new Font("Segoe UI",20,FontStyle.Bold),AutoEllipsis=true};
  panel.Controls.AddRange(new Control[]{caption,value});return panel;
 }

 Button DashboardButton(string text,int left,int top,int width){var button=new Button{Text=text,Left=left,Top=top,Width=width,Height=38};XdolfTheme.StyleButton(button);return button;}

 void LayoutDashboardHome(){
  if(!homeDashboardReady||homeView.ClientSize.Width<=0)return;
  int margin=30,gap=14,available=Math.Max(520,homeView.ClientSize.Width-margin*2);
  dashboardLibrary.Width=Math.Max(300,available);
  int columns=available<760?2:4;
  int statWidth=(available-gap*(columns-1))/columns;
  Panel[] stats={statCollections,statDownloads,statStreams,statOnline};
  for(int i=0;i<stats.Length;i++){
   int row=i/columns,col=i%columns;
   stats[i].SetBounds(margin+col*(statWidth+gap),122+row*126,statWidth,112);
   foreach(Label label in stats[i].Controls.OfType<Label>())label.Width=Math.Max(80,statWidth-30);
  }
  int rows=(stats.Length+columns-1)/columns;
  int quickTitleTop=122+rows*126+22;
  dashboardQuickTitle.Top=quickTitleTop;
  dashboardQuickPanel.SetBounds(margin,quickTitleTop+39,available,available<720?176:126);
  int buttonGap=12,buttonCount=available<720?2:4,buttonWidth=(available-40-buttonGap*(buttonCount-1))/buttonCount;
  Button[] buttons={dashboardCollectionsButton,dashboardSearchButton,dashboardAddButton,dashboardLibraryButton};
  for(int i=0;i<buttons.Length;i++){
   int row=i/buttonCount,col=i%buttonCount;
   buttons[i].SetBounds(20+col*(buttonWidth+buttonGap),23+row*50,buttonWidth,38);
  }
  dashboardHint.SetBounds(20,dashboardQuickPanel.Height-43,Math.Max(120,available-40),26);
  int infoTop=dashboardQuickPanel.Bottom+40;
  dashboardInfoTitle.Top=infoTop;
  dashboardInfoPanel.SetBounds(margin,infoTop+39,available,118);
  var info=dashboardInfoPanel.Controls.OfType<Label>().FirstOrDefault(l=>l.Name=="DashboardInfoText");if(info!=null)info.Width=Math.Max(150,available-40);
 }

 void RefreshDashboardHome(){
  if(!homeDashboardReady)return;
  dashboardLibrary.Text=root.Length==0?"No library folder selected":root;
  dashboardCollections.Text=people.Count.ToString();
  dashboardDownloads.Text=TransferBridge.ActiveDownloads.ToString();
  dashboardStreams.Text=TransferBridge.ActiveStreams.ToString();
  dashboardOnline.Text=onlineSettings.Configured?"CUSTOM":"BUILT-IN";
  dashboardAddButton.Enabled=DirectoryExists(root);
  dashboardSearchButton.Enabled=true;
  dashboardHint.Text=root.Length==0?"Choose a library folder, then add or browse collections.":people.Count==0?"Your library is ready. Add your first collection to begin.":people.Count+" collection"+(people.Count==1?" is":"s are")+" available. Use Collections to browse them.";
 }

 bool DirectoryExists(string path){try{return !string.IsNullOrWhiteSpace(path)&&System.IO.Directory.Exists(path);}catch{return false;}}

 void HideAllMainViews(){foreach(Control v in new Control[]{homeView,collectionsBrowserView,collectionView,searchView,downloadsView,streamingView,settingsView})v.Visible=false;}

 internal void ShowDashboardHome(){
  EnsureHomeDashboard();HideAllMainViews();section=ShellSection.Home;homeView.Visible=true;homeView.BringToFront();
  navHome.SetActive(true);navSearch.SetActive(false);navCollections.SetActive(false);navDownloads.SetActive(false);navStreaming.SetActive(false);navSettings.SetActive(false);
  LayoutDashboardHome();RefreshDashboardHome();RefreshActivityBadges();string count=people.Count+" collection"+(people.Count==1?"":"s");SetStatus("Ready",root.Length==0?"Choose a library folder to begin.":count);
 }

 internal void ShowCollectionsBrowser(){
  EnsureHomeDashboard();HideAllMainViews();section=ShellSection.Collections;collectionsBrowserView.Visible=true;collectionsBrowserView.BringToFront();
  navHome.SetActive(false);navSearch.SetActive(false);navCollections.SetActive(true);navDownloads.SetActive(false);navStreaming.SetActive(false);navSettings.SetActive(false);
  homeHeading.Text="Collections";homePath.Text=root.Length==0?"Choose a folder to get started":root;RenderHome();RefreshActivityBadges();
 }

 internal Control CollectionsBrowserForCapture(){return collectionsBrowserView;}
}
}
