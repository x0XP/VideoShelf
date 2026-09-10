using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 readonly Panel collectionsBrowserView=new Panel();
 Label dashboardLibrary,dashboardCollections,dashboardDownloads,dashboardStreams,dashboardOnline,dashboardHint;
 Button dashboardCollectionsButton,dashboardSearchButton,dashboardAddButton,dashboardLibraryButton;
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

  navHome.Click+=delegate{ShowDashboardHome();};
  navCollections.Click+=delegate{ShowCollectionsBrowser();};
  back.Click+=delegate{ShowCollectionsBrowser();};
  homeView.VisibleChanged+=delegate{if(homeView.Visible&&section==ShellSection.Collections)BeginInvoke((MethodInvoker)ShowCollectionsBrowser);};
  statusRight.TextChanged+=delegate{if(section==ShellSection.Home)RefreshDashboardHome();};
  statusLeft.TextChanged+=delegate{if(section==ShellSection.Home)RefreshDashboardHome();};

  RefreshDashboardHome();
 }

 void BuildDashboardHome(){
  var heading=new Label{Text="Home",Left=30,Top=26,Width=500,Height=40,ForeColor=Color.White,Font=new Font("Segoe UI",23,FontStyle.Bold)};
  dashboardLibrary=new Label{Left=31,Top=69,Width=900,Height=23,ForeColor=XdolfTheme.Muted,AutoEllipsis=true};
  homeView.Controls.AddRange(new Control[]{heading,dashboardLibrary});

  int top=122;
  homeView.Controls.Add(MakeDashboardStat("COLLECTIONS",out dashboardCollections,30,top));
  homeView.Controls.Add(MakeDashboardStat("ACTIVE DOWNLOADS",out dashboardDownloads,250,top));
  homeView.Controls.Add(MakeDashboardStat("ACTIVE STREAMS",out dashboardStreams,470,top));
  homeView.Controls.Add(MakeDashboardStat("ONLINE SEARCH",out dashboardOnline,690,top));

  var quickTitle=new Label{Text="Quick actions",Left=30,Top=274,Width=300,Height=28,ForeColor=Color.White,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  var quickPanel=new Panel{Left=30,Top=313,Width=880,Height=126,BackColor=Color.FromArgb(9,18,26),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
  quickPanel.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,quickPanel.Width-1,quickPanel.Height-1);using(var b=new SolidBrush(XdolfTheme.AccentBlue))e.Graphics.FillRectangle(b,0,0,quickPanel.Width,2);using(var b=new SolidBrush(XdolfTheme.AccentRed))e.Graphics.FillRectangle(b,0,2,3,quickPanel.Height-2);};

  dashboardCollectionsButton=DashboardButton("Browse collections",20,23,190);
  dashboardSearchButton=DashboardButton("Search online",222,23,165);
  dashboardAddButton=DashboardButton("+ Add collection",399,23,175);
  dashboardLibraryButton=DashboardButton("Change library",586,23,160);
  dashboardCollectionsButton.Click+=delegate{ShowCollectionsBrowser();};
  dashboardSearchButton.Click+=delegate{ShowSearch();};
  dashboardAddButton.Click+=delegate{AddLibraryFolder();};
  dashboardLibraryButton.Click+=delegate{choose.PerformClick();};
  quickPanel.Controls.AddRange(new Control[]{dashboardCollectionsButton,dashboardSearchButton,dashboardAddButton,dashboardLibraryButton});

  dashboardHint=new Label{Left=20,Top=77,Width=830,Height=28,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",9f),AutoEllipsis=true};
  quickPanel.Controls.Add(dashboardHint);
  homeView.Controls.AddRange(new Control[]{quickTitle,quickPanel});

  var infoTitle=new Label{Text="Library overview",Left=30,Top=480,Width=300,Height=28,ForeColor=Color.White,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  var info=new Panel{Left=30,Top=519,Width=880,Height=118,BackColor=Color.FromArgb(8,17,25),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
  info.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,info.Width-1,info.Height-1);};
  var infoText=new Label{Text="Collections are folders inside your selected VideoShelf library. Open Collections to browse artwork and local videos, or Search to find seeded torrent metadata. Online search never starts a media transfer until you explicitly choose Download or Stream.",Left=20,Top=20,Width=830,Height=72,ForeColor=XdolfTheme.Text,Font=new Font("Segoe UI",9.5f)};
  info.Controls.Add(infoText);homeView.Controls.AddRange(new Control[]{infoTitle,info});
 }

 Panel MakeDashboardStat(string title,out Label value,int left,int top){
  var panel=new Panel{Left=left,Top=top,Width=200,Height=112,BackColor=Color.FromArgb(9,18,26)};
  panel.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,panel.Width-1,panel.Height-1);using(var b=new SolidBrush(XdolfTheme.AccentBlue))e.Graphics.FillRectangle(b,0,0,panel.Width,2);};
  var caption=new Label{Text=title,Left=15,Top=15,Width=170,Height=18,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",8f,FontStyle.Bold)};
  value=new Label{Text="—",Left=15,Top=43,Width=170,Height=42,ForeColor=Color.White,Font=new Font("Segoe UI",20,FontStyle.Bold),AutoEllipsis=true};
  panel.Controls.AddRange(new Control[]{caption,value});return panel;
 }

 Button DashboardButton(string text,int left,int top,int width){
  var button=new Button{Text=text,Left=left,Top=top,Width=width,Height=38};
  XdolfTheme.StyleButton(button);return button;
 }

 void RefreshDashboardHome(){
  if(!homeDashboardReady)return;
  dashboardLibrary.Text=root.Length==0?"No library folder selected":root;
  dashboardCollections.Text=people.Count.ToString();
  dashboardDownloads.Text=TransferBridge.ActiveDownloads.ToString();
  dashboardStreams.Text=TransferBridge.ActiveStreams.ToString();
  dashboardOnline.Text=onlineSettings.Configured?"CUSTOM":"BUILT-IN";
  dashboardOnline.Font=new Font("Segoe UI",onlineSettings.Configured?13f:13f,FontStyle.Bold);
  dashboardAddButton.Enabled=DirectoryExists(root);
  dashboardSearchButton.Enabled=true;
  dashboardHint.Text=root.Length==0?"Choose a library folder, then add or browse collections.":people.Count==0?"Your library is ready. Add your first collection to begin.":people.Count+" collection"+(people.Count==1?" is":"s are")+" available. Use Collections to browse them.";
 }

 bool DirectoryExists(string path){try{return !string.IsNullOrWhiteSpace(path)&&System.IO.Directory.Exists(path);}catch{return false;}}

 void HideAllMainViews(){
  foreach(Control v in new Control[]{homeView,collectionsBrowserView,collectionView,searchView,downloadsView,streamingView,settingsView})v.Visible=false;
 }

 internal void ShowDashboardHome(){
  EnsureHomeDashboard();HideAllMainViews();section=ShellSection.Home;homeView.Visible=true;homeView.BringToFront();
  navHome.SetActive(true);navSearch.SetActive(false);navCollections.SetActive(false);navDownloads.SetActive(false);navStreaming.SetActive(false);navSettings.SetActive(false);
  RefreshDashboardHome();RefreshActivityBadges();SetStatus("Ready",root.Length==0?"Choose a library folder to begin.":people.Count+" collections");
 }

 internal void ShowCollectionsBrowser(){
  EnsureHomeDashboard();HideAllMainViews();section=ShellSection.Collections;collectionsBrowserView.Visible=true;collectionsBrowserView.BringToFront();
  navHome.SetActive(false);navSearch.SetActive(false);navCollections.SetActive(true);navDownloads.SetActive(false);navStreaming.SetActive(false);navSettings.SetActive(false);
  homeHeading.Text="Collections";homePath.Text=root.Length==0?"Choose a folder to get started":root;RenderHome();RefreshActivityBadges();
 }

 internal Control CollectionsBrowserForCapture(){return collectionsBrowserView;}
}
}
