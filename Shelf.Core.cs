using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VideoShelf {
sealed partial class Shelf : Form {
 readonly Panel body=new Panel(),sidebar=new Panel(),mainHost=new Panel();
 readonly Panel homeView=new Panel(),collectionView=new Panel(),searchView=new Panel(),downloadsView=new Panel(),streamingView=new Panel(),settingsView=new Panel();
 readonly FlowLayoutPanel cards=new FlowLayoutPanel(),onlineCards=new FlowLayoutPanel();
 readonly ListView files=new ListView();
 readonly Label homeHeading=new Label(),homePath=new Label(),collectionTitle=new Label(),collectionPath=new Label(),searchHeading=new Label(),searchCount=new Label(),inspectorTitle=new Label(),inspectorMeta=new Label(),settingsPath=new Label();
 readonly PictureBox inspectorImage=new PictureBox();readonly FlowLayoutPanel inspectorTags=new FlowLayoutPanel();readonly Panel inspector=new Panel();
 readonly TextBox libraryFilter=new TextBox(),onlineQuery=new TextBox();readonly ComboBox homeSort=new ComboBox(),sourceFilter=new ComboBox(),categoryFilter=new ComboBox(),resolution=new ComboBox();
 readonly Button choose=new Button(),addFolder=new Button(),refresh=new Button(),back=new Button(),playLocal=new Button(),openFolder=new Button(),findOnline=new Button(),searchOnline=new Button(),configure=new Button(),streamOnline=new Button(),downloadOnline=new Button(),copyLink=new Button(),viewFiles=new Button(),settingsSource=new Button();
 readonly NavButton navHome=new NavButton("Home","\uE80F"),navSearch=new NavButton("Search","\uE721"),navCollections=new NavButton("Collections","\uE8B7"),navDownloads=new NavButton("Downloads","\uE896"),navStreaming=new NavButton("Streaming","\uE768"),navSettings=new NavButton("Settings","\uE713");
 readonly Label statusLeft=new Label(),statusRight=new Label();readonly ToolTip tips=new ToolTip();readonly Timer activityTimer=new Timer();
 System.Threading.CancellationTokenSource portraitScan=new System.Threading.CancellationTokenSource(),onlineScan=new System.Threading.CancellationTokenSource(),thumbnailScan=new System.Threading.CancellationTokenSource();
 bool fetchingPortraits=false,onlineSearching=false,thumbnailsLoading=false,thumbnailsPaused=false,suppress=false;int generation=0,skipped=0;
 List<Person> people=new List<Person>();List<Video> videos=new List<Video>();List<OnlineResult> onlineResults=new List<OnlineResult>();readonly HashSet<string> thumbnailAttempted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 string root="",onlineError="",onlineQueryFor="";Person current;OnlineResult selectedOnline;OnlineSettings onlineSettings=OnlineSettings.Load();ShellSection section=ShellSection.Home;
 readonly string settings=AppDataPaths.MigrateFile("directory.txt");

 public Shelf(){
  XdolfTheme.ConfigureToolTip(tips);Text="VideoShelf";Size=new Size(1366,860);MinimumSize=new Size(1080,680);StartPosition=FormStartPosition.CenterScreen;BackColor=XdolfTheme.Outline;ForeColor=XdolfTheme.Text;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;KeyPreview=true;FormBorderStyle=FormBorderStyle.None;Padding=new Padding(1);
  BuildBody();BuildSidebar();BuildHome();BuildCollection();BuildSearch();BuildActivityViews();BuildSettings();BuildStatusBar();BuildTitleBar();WireEvents();ShowSection(homeView,ShellSection.Home);
  activityTimer.Interval=750;activityTimer.Tick+=delegate{RefreshActivityBadges();if(section==ShellSection.Downloads||section==ShellSection.Streaming)RenderActivity();};activityTimer.Start();
  Shown+=delegate{try{if(File.Exists(settings)&&Directory.Exists(File.ReadAllText(settings)))LoadRoot(File.ReadAllText(settings));else SetStatus("Ready","Choose a library folder to begin.");}catch{SetStatus("Ready","Choose a library folder to begin.");}};
  FormClosed+=delegate{generation++;portraitScan.Cancel();onlineScan.Cancel();thumbnailScan.Cancel();portraitScan.Dispose();onlineScan.Dispose();thumbnailScan.Dispose();activityTimer.Dispose();tips.Dispose();if(inspectorImage.Image!=null)inspectorImage.Image.Dispose();foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();};
 }

 void BuildBody(){
  body.Dock=DockStyle.Fill;body.BackColor=XdolfTheme.Background;Controls.Add(body);
  sidebar.Dock=DockStyle.Left;sidebar.Width=244;sidebar.BackColor=XdolfTheme.Sidebar;mainHost.Dock=DockStyle.Fill;mainHost.BackColor=XdolfTheme.Background;body.Controls.Add(mainHost);body.Controls.Add(sidebar);
  sidebar.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Color.FromArgb(31,49,63)))e.Graphics.DrawLine(p,sidebar.Width-1,0,sidebar.Width-1,sidebar.Height);};
  foreach(var v in new[]{homeView,collectionView,searchView,downloadsView,streamingView,settingsView}){v.Dock=DockStyle.Fill;v.BackColor=XdolfTheme.Background;v.Visible=false;mainHost.Controls.Add(v);}
 }
 void BuildTitleBar(){
  var bar=new Panel{Dock=DockStyle.Top,Height=34,BackColor=Color.FromArgb(5,12,18)};Controls.Add(bar);bar.BringToFront();
  var icon=new Label{Text="◈",Left=10,Top=5,Width=23,Height=23,ForeColor=XdolfTheme.AccentBlue,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI Symbol",11,FontStyle.Bold)};
  var caption=new Label{Text="VideoShelf v1.7",Left=38,Top=7,Width=220,Height=20,ForeColor=XdolfTheme.TextStrong,Font=new Font("Segoe UI",9.5f)};
  Button min=WindowButton("—"),max=WindowButton("□"),close=WindowButton("×");min.Left=Width-135;max.Left=Width-90;close.Left=Width-45;foreach(var b in new[]{min,max,close}){b.Anchor=AnchorStyles.Top|AnchorStyles.Right;bar.Controls.Add(b);}bar.Controls.AddRange(new Control[]{icon,caption});
  min.Click+=delegate{WindowState=FormWindowState.Minimized;};max.Click+=delegate{ToggleMaximize();};close.Click+=delegate{Close();};
  MouseEventHandler drag=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,0x2,0);}};bar.MouseDown+=drag;caption.MouseDown+=drag;icon.MouseDown+=drag;bar.DoubleClick+=delegate{ToggleMaximize();};
 }
 void BuildStatusBar(){
  var bar=new Panel{Dock=DockStyle.Bottom,Height=29,BackColor=Color.FromArgb(6,14,21)};Controls.Add(bar);bar.BringToFront();bar.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Color.FromArgb(27,43,56)))e.Graphics.DrawLine(p,0,0,bar.Width,0);};
  statusLeft.SetBounds(18,6,500,18);statusLeft.ForeColor=XdolfTheme.Muted;statusLeft.Font=new Font("Segoe UI",8.4f);statusLeft.Text="Ready";
  statusRight.SetBounds(540,6,780,18);statusRight.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;statusRight.TextAlign=ContentAlignment.MiddleRight;statusRight.ForeColor=XdolfTheme.Muted;statusRight.Font=new Font("Segoe UI",8.4f);bar.Controls.AddRange(new Control[]{statusLeft,statusRight});
 }
 Button WindowButton(string text){var b=new Button{Text=text,Top=0,Width=45,Height=33,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(5,12,18),ForeColor=XdolfTheme.Text,TabStop=false};b.FlatAppearance.BorderSize=0;b.FlatAppearance.MouseOverBackColor=text=="×"?Color.FromArgb(122,32,38):XdolfTheme.Hover;return b;}
 void ToggleMaximize(){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}

 void BuildSidebar(){
  var logo=new LogoMark{Left=23,Top=24};var name=new Label{Text="VideoShelf",Left=99,Top=34,Width=132,Height=30,ForeColor=Color.White,Font=new Font("Segoe UI",16,FontStyle.Bold)};sidebar.Controls.AddRange(new Control[]{logo,name});
  int y=128;foreach(var n in new[]{navHome,navSearch,navCollections,navDownloads,navStreaming}){n.Left=0;n.Top=y;n.Width=244;sidebar.Controls.Add(n);y+=54;}navSettings.Left=0;navSettings.Top=y+8;navSettings.Width=244;sidebar.Controls.Add(navSettings);
 }
 void BuildHome(){
  homeHeading.SetBounds(24,24,500,36);homeHeading.Text="Home";homeHeading.ForeColor=Color.White;homeHeading.Font=new Font("Segoe UI",22,FontStyle.Bold);homeView.Controls.Add(homeHeading);
  homePath.SetBounds(25,65,800,22);homePath.ForeColor=XdolfTheme.Muted;homePath.AutoEllipsis=true;homePath.Text="Choose a folder to get started";homeView.Controls.Add(homePath);
  Style(choose,"Choose folder",24,101,132);Style(addFolder,"+ Add collection",166,101,135);Style(refresh,"Refresh",311,101,92);addFolder.Enabled=false;homeView.Controls.AddRange(new Control[]{choose,addFolder,refresh});
  libraryFilter.SetBounds(423,103,265,31);StyleInput(libraryFilter,"Filter collections");homeSort.SetBounds(700,101,160,32);homeSort.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(homeSort);homeSort.Items.AddRange(new object[]{"Name A–Z","Name Z–A"});homeSort.SelectedIndex=0;homeView.Controls.AddRange(new Control[]{libraryFilter,homeSort});
  var rule=new Panel{Left=24,Top=153,Height=1,Width=900,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Color.FromArgb(28,45,58)};homeView.Controls.Add(rule);
  cards.SetBounds(24,174,homeView.Width-44,homeView.Height-184);cards.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;cards.AutoScroll=true;cards.WrapContents=true;cards.BackColor=XdolfTheme.Background;cards.Padding=new Padding(0,0,0,15);homeView.Controls.Add(cards);
 }
 void BuildCollection(){
  Style(back,"← Back",24,22,92);collectionView.Controls.Add(back);collectionTitle.SetBounds(24,68,700,38);collectionTitle.ForeColor=Color.White;collectionTitle.Font=new Font("Segoe UI",23,FontStyle.Bold);collectionView.Controls.Add(collectionTitle);collectionPath.SetBounds(25,108,850,22);collectionPath.ForeColor=XdolfTheme.Muted;collectionPath.AutoEllipsis=true;collectionView.Controls.Add(collectionPath);
  Style(playLocal,"▶ Play selected",24,148,145);Style(openFolder,"Open folder",179,148,112);Style(findOnline,"Find online",301,148,112);XdolfTheme.StylePrimary(findOnline);collectionView.Controls.AddRange(new Control[]{playLocal,openFolder,findOnline});
  files.SetBounds(24,202,collectionView.Width-48,collectionView.Height-224);files.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;files.View=View.Details;files.FullRowSelect=true;files.MultiSelect=false;files.HideSelection=false;files.BackColor=Color.FromArgb(8,17,25);files.ForeColor=XdolfTheme.Text;files.BorderStyle=BorderStyle.FixedSingle;files.Columns.Add("VIDEO",500);files.Columns.Add("TYPE",80);files.Columns.Add("SIZE",110);files.Columns.Add("MODIFIED",160);files.Columns.Add("SUBFOLDER",220);collectionView.Controls.Add(files);
 }
 void BuildSearch(){
  var resultsHost=new Panel{Dock=DockStyle.Fill,BackColor=XdolfTheme.Background};searchView.Controls.Add(resultsHost);
  inspector.Dock=DockStyle.Right;inspector.Width=332;inspector.Padding=new Padding(13);inspector.BackColor=Color.FromArgb(7,16,23);searchView.Controls.Add(inspector);inspector.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Color.FromArgb(31,49,63)))e.Graphics.DrawRectangle(p,0,0,inspector.Width-1,inspector.Height-1);};
  var filterBar=new Panel{Dock=DockStyle.Top,Height=68,BackColor=Color.FromArgb(6,14,21)};searchView.Controls.Add(filterBar);filterBar.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Color.FromArgb(31,49,63)))e.Graphics.DrawLine(p,0,filterBar.Height-1,filterBar.Width,filterBar.Height-1);};filterBar.BringToFront();inspector.BringToFront();
  onlineQuery.SetBounds(17,14,465,38);StyleInput(onlineQuery,"Search");filterBar.Controls.Add(onlineQuery);
  sourceFilter.SetBounds(496,13,150,40);sourceFilter.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(sourceFilter);sourceFilter.Items.Add("All sources");sourceFilter.SelectedIndex=0;filterBar.Controls.Add(sourceFilter);
  categoryFilter.SetBounds(658,13,125,40);categoryFilter.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(categoryFilter);categoryFilter.Items.AddRange(new object[]{"All categories","Anime","TV","Movies","Other"});categoryFilter.SelectedIndex=0;filterBar.Controls.Add(categoryFilter);
  resolution.SetBounds(795,13,140,40);resolution.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(resolution);resolution.Items.AddRange(new object[]{"All resolutions","2160p","1080p","720p","Other"});resolution.SelectedIndex=0;filterBar.Controls.Add(resolution);
  Style(searchOnline,"Search",947,13,108);searchOnline.Height=40;XdolfTheme.StylePrimary(searchOnline);filterBar.Controls.Add(searchOnline);Style(configure,"Source",1066,13,84);configure.Height=40;filterBar.Controls.Add(configure);

  inspectorImage.SetBounds(13,14,306,205);inspectorImage.SizeMode=PictureBoxSizeMode.Zoom;inspectorImage.BackColor=Color.FromArgb(13,27,38);inspector.Controls.Add(inspectorImage);
  inspectorTitle.SetBounds(13,233,306,62);inspectorTitle.ForeColor=Color.White;inspectorTitle.Font=new Font("Segoe UI",13.5f,FontStyle.Bold);inspectorTitle.AutoEllipsis=true;inspector.Controls.Add(inspectorTitle);
  inspectorTags.SetBounds(13,302,306,58);inspectorTags.WrapContents=true;inspectorTags.AutoScroll=false;inspectorTags.BackColor=Color.Transparent;inspector.Controls.Add(inspectorTags);
  inspectorMeta.SetBounds(13,369,306,214);inspectorMeta.ForeColor=Color.FromArgb(183,199,215);inspectorMeta.Font=new Font("Segoe UI",9.3f);inspector.Controls.Add(inspectorMeta);
  Style(downloadOnline,"↓  Download locally",13,592,306);downloadOnline.Height=62;downloadOnline.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;XdolfTheme.StylePrimary(downloadOnline);inspector.Controls.Add(downloadOnline);
  Style(streamOnline,"▶  Stream locally",13,666,306);streamOnline.Height=62;streamOnline.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;XdolfTheme.StyleDanger(streamOnline);inspector.Controls.Add(streamOnline);
  Style(copyLink,"Copy link",13,740,147);copyLink.Height=47;copyLink.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;Style(viewFiles,"View files",172,740,147);viewFiles.Height=47;viewFiles.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;inspector.Controls.AddRange(new Control[]{copyLink,viewFiles});

  var head=new Panel{Dock=DockStyle.Top,Height=78,BackColor=XdolfTheme.Background};resultsHost.Controls.Add(head);searchHeading.SetBounds(16,14,610,30);searchHeading.ForeColor=Color.White;searchHeading.Font=new Font("Segoe UI",15.5f,FontStyle.Bold);searchHeading.Text="Search";head.Controls.Add(searchHeading);searchCount.SetBounds(17,46,500,20);searchCount.ForeColor=XdolfTheme.Muted;searchCount.Text="Search a configured metadata source.";head.Controls.Add(searchCount);var note=new Label{Text="No media is downloaded until you choose\nto download or stream.",Width=280,Height=42,Top=15,Left=resultsHost.Width-300,Anchor=AnchorStyles.Top|AnchorStyles.Right,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",8.4f),TextAlign=ContentAlignment.TopRight};head.Controls.Add(note);
  onlineCards.Dock=DockStyle.Fill;onlineCards.AutoScroll=true;onlineCards.WrapContents=false;onlineCards.FlowDirection=FlowDirection.TopDown;onlineCards.Padding=new Padding(16,0,10,16);onlineCards.BackColor=XdolfTheme.Background;resultsHost.Controls.Add(onlineCards);onlineCards.BringToFront();onlineCards.Resize+=delegate{ResizeOnlineCards();};
 }
 void BuildActivityViews(){BuildActivityView(downloadsView,"Downloads","Persistent downloads started from VideoShelf.");BuildActivityView(streamingView,"Streaming","Active in-app torrent streaming sessions.");}
 void BuildActivityView(Panel view,string heading,string sub){
  var h=new Label{Text=heading,Left=24,Top=28,Width=500,Height=38,ForeColor=Color.White,Font=new Font("Segoe UI",22,FontStyle.Bold)};var s=new Label{Text=sub,Left=25,Top=69,Width=700,Height=23,ForeColor=XdolfTheme.Muted};view.Controls.AddRange(new Control[]{h,s});
  var flow=new FlowLayoutPanel{Left=24,Top=112,Width=view.Width-48,Height=view.Height-132,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=XdolfTheme.Background};view.Controls.Add(flow);
 }
 void BuildSettings(){
  var h=new Label{Text="Settings",Left=24,Top=28,Width=500,Height=38,ForeColor=Color.White,Font=new Font("Segoe UI",22,FontStyle.Bold)};var s=new Label{Text="Library and online metadata source",Left=25,Top=69,Width=600,Height=23,ForeColor=XdolfTheme.Muted};settingsView.Controls.AddRange(new Control[]{h,s});
  var box=new Panel{Left=24,Top=112,Width=760,Height=180,BackColor=XdolfTheme.PanelRaised};box.Paint+=delegate(object sender,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,box.Width-1,box.Height-1);};var lh=new Label{Text="Library folder",Left=20,Top=20,Width=180,Height=23,ForeColor=Color.White,Font=new Font("Segoe UI",10.5f,FontStyle.Bold)};settingsPath.SetBounds(20,51,710,42);settingsPath.ForeColor=XdolfTheme.Muted;settingsPath.AutoEllipsis=true;Style(settingsSource,"Configure online source",20,111,190);box.Controls.AddRange(new Control[]{lh,settingsPath,settingsSource});settingsView.Controls.Add(box);
 }
 void WireEvents(){
  navHome.Click+=delegate{ShowHome();};navCollections.Click+=delegate{ShowCollections();};navSearch.Click+=delegate{ShowSearch();};navDownloads.Click+=delegate{ShowDownloads();};navStreaming.Click+=delegate{ShowStreaming();};navSettings.Click+=delegate{ShowSettings();};
  choose.Click+=delegate{using(var d=new FolderBrowserDialog{Description="Choose the parent folder containing your VideoShelf collections",SelectedPath=root,ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK)LoadRoot(d.SelectedPath);};addFolder.Click+=delegate{AddLibraryFolder();};refresh.Click+=delegate{if(root.Length>0)LoadRoot(root);};libraryFilter.TextChanged+=delegate{if(!suppress)RenderHome();};homeSort.SelectedIndexChanged+=delegate{if(!suppress)RenderHome();};
  back.Click+=delegate{ShowCollections();};playLocal.Click+=delegate{Play();};files.DoubleClick+=delegate{Play();};files.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){Play();e.Handled=true;}};openFolder.Click+=delegate{if(current!=null)Launch(current.Path);};findOnline.Click+=delegate{ShowSearch();if(current!=null){onlineQuery.Text=current.Name;SearchOnline(current.Name,true);}};
  searchOnline.Click+=delegate{SearchOnline(onlineQuery.Text,true);};onlineQuery.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){SearchOnline(onlineQuery.Text,true);e.SuppressKeyPress=true;}};configure.Click+=delegate{ConfigureOnline();};settingsSource.Click+=delegate{ConfigureOnline();};sourceFilter.SelectedIndexChanged+=delegate{if(!suppress)RenderOnline();};categoryFilter.SelectedIndexChanged+=delegate{if(!suppress)RenderOnline();};resolution.SelectedIndexChanged+=delegate{if(!suppress)RenderOnline();};downloadOnline.Click+=delegate{DownloadOnline();};streamOnline.Click+=delegate{StreamOnline();};copyLink.Click+=delegate{CopyOnline();};viewFiles.Click+=delegate{ViewOnlineFiles();};
  KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.F){if(section==ShellSection.Search)onlineQuery.Focus();else if(section==ShellSection.Home||section==ShellSection.Collections)libraryFilter.Focus();e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.N&&(section==ShellSection.Home||section==ShellSection.Collections)){AddLibraryFolder();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F5){if(section==ShellSection.Search)SearchOnline(onlineQuery.Text,true);else if(root.Length>0)LoadRoot(root);}if(e.KeyCode==Keys.Escape&&section==ShellSection.Search&&current!=null)OpenPerson(current);};
 }
 void ShowSection(Control view,ShellSection target){section=target;foreach(var v in new[]{homeView,collectionView,searchView,downloadsView,streamingView,settingsView})v.Visible=v==view;view.BringToFront();navHome.SetActive(target==ShellSection.Home);navSearch.SetActive(target==ShellSection.Search);navCollections.SetActive(target==ShellSection.Collections);navDownloads.SetActive(target==ShellSection.Downloads);navStreaming.SetActive(target==ShellSection.Streaming);navSettings.SetActive(target==ShellSection.Settings);RefreshActivityBadges();}
 internal void ShowHome(){homeHeading.Text="Home";ShowSection(homeView,ShellSection.Home);RenderHome();SetStatus("Ready",root.Length==0?"Choose a library folder to begin.":people.Count+" collections");}
 internal void ShowCollections(){homeHeading.Text="Collections";ShowSection(homeView,ShellSection.Collections);RenderHome();SetStatus("Ready",people.Count+" collections");}
 internal void ShowSearch(){ShowSection(searchView,ShellSection.Search);if(current!=null&&string.IsNullOrWhiteSpace(onlineQuery.Text))onlineQuery.Text=current.Name;RenderOnline();}
 internal void ShowDownloads(){ShowSection(downloadsView,ShellSection.Downloads);RenderActivity();}
 internal void ShowStreaming(){ShowSection(streamingView,ShellSection.Streaming);RenderActivity();}
 internal void ShowSettings(){settingsPath.Text=root.Length==0?"No library folder selected":root;ShowSection(settingsView,ShellSection.Settings);SetStatus("Settings",onlineSettings.Configured?"Online metadata source configured":"Online metadata source not configured");}
 void SetStatus(string left,string right){statusLeft.Text=left??"";statusRight.Text=right??"";}
 void RefreshActivityBadges(){navDownloads.SetBadge(TransferBridge.ActiveDownloads);navStreaming.SetBadge(TransferBridge.ActiveStreams);}
 void RenderActivity(){
  Panel view=section==ShellSection.Streaming?streamingView:downloadsView;var flow=view.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();if(flow==null)return;flow.SuspendLayout();while(flow.Controls.Count>0)flow.Controls[0].Dispose();var items=TransferBridge.Snapshot(section==ShellSection.Streaming?"stream":"download");
  foreach(var a in items){var p=new Panel{Width=Math.Max(600,flow.ClientSize.Width-28),Height=78,BackColor=XdolfTheme.PanelRaised,Margin=new Padding(0,0,0,10)};p.Paint+=delegate(object s,PaintEventArgs e){using(var pen=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(pen,0,0,p.Width-1,p.Height-1);};var t=new Label{Text=a.Title,Left=16,Top=12,Width=p.Width-180,Height=24,ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold),AutoEllipsis=true};var m=new Label{Text=(a.Mode=="stream"?"Streaming":"Downloading")+" • "+a.Started.ToLocalTime().ToString("HH:mm:ss"),Left=16,Top=42,Width=p.Width-32,Height=20,ForeColor=XdolfTheme.Muted};p.Controls.AddRange(new Control[]{t,m});flow.Controls.Add(p);}if(items.Count==0)flow.Controls.Add(new Label{Text=section==ShellSection.Streaming?"No active streams.":"No active downloads.",Width=600,Height=40,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",11)});flow.ResumeLayout();SetStatus("Ready",items.Count+" active "+(section==ShellSection.Streaming?"streams":"downloads"));
 }
 void ResizeOnlineCards(){int w=Math.Max(560,onlineCards.ClientSize.Width-onlineCards.Padding.Left-onlineCards.Padding.Right-24);foreach(Control c in onlineCards.Controls)c.Width=w;}
 void Style(Button b,string text,int x,int y,int width){b.Text=text;b.SetBounds(x,y,width,34);XdolfTheme.StyleButton(b);}
 void StyleInput(TextBox box,string accessible){XdolfTheme.StyleInput(box);box.BorderStyle=BorderStyle.FixedSingle;box.Font=new Font("Segoe UI",10);box.AccessibleName=accessible;}
 void StyleCombo(ComboBox box){XdolfTheme.StyleInput(box);box.FlatStyle=FlatStyle.Flat;box.Font=new Font("Segoe UI",9.5f);}
 internal static string SizeText(long n){if(n<=0)return "—";return n>=1099511627776L?(n/1099511627776d).ToString("0.0")+" TB":n>=1073741824?(n/1073741824d).ToString("0.0")+" GB":(n/1048576d).ToString("0.0")+" MB";}
 void Launch(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,"Could not open this item.\n\n"+ex.Message,"VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
 protected override void WndProc(ref Message m){const int WM_NCHITTEST=0x84,HTLEFT=10,HTRIGHT=11,HTTOP=12,HTTOPLEFT=13,HTTOPRIGHT=14,HTBOTTOM=15,HTBOTTOMLEFT=16,HTBOTTOMRIGHT=17;if(m.Msg==WM_NCHITTEST&&WindowState==FormWindowState.Normal){base.WndProc(ref m);long lp=m.LParam.ToInt64();var p=PointToClient(new Point(unchecked((short)(lp&0xffff)),unchecked((short)((lp>>16)&0xffff))));int grip=7;bool l=p.X<grip,r=p.X>ClientSize.Width-grip,t=p.Y<grip,b=p.Y>ClientSize.Height-grip;if(l&&t){m.Result=(IntPtr)HTTOPLEFT;return;}if(r&&t){m.Result=(IntPtr)HTTOPRIGHT;return;}if(l&&b){m.Result=(IntPtr)HTBOTTOMLEFT;return;}if(r&&b){m.Result=(IntPtr)HTBOTTOMRIGHT;return;}if(l){m.Result=(IntPtr)HTLEFT;return;}if(r){m.Result=(IntPtr)HTRIGHT;return;}if(t){m.Result=(IntPtr)HTTOP;return;}if(b){m.Result=(IntPtr)HTBOTTOM;return;}return;}base.WndProc(ref m);}
 [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hWnd,int msg,int wParam,int lParam);
}
}
