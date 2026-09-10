using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VideoShelf {
sealed partial class Shelf : Form {
 readonly Color bg=XdolfTheme.Background, surface=XdolfTheme.PanelRaised, muted=XdolfTheme.Muted;
 readonly FlowLayoutPanel cards=new FlowLayoutPanel(); readonly Panel content=new Panel(), detail=new Panel(), localDetail=new Panel(), onlineDetail=new Panel();
 readonly Label title=new Label(), subtitle=new Label(), status=new Label(); readonly TextBox search=new TextBox(), onlineQuery=new TextBox(); readonly ComboBox sort=new ComboBox(), resolution=new ComboBox();
 readonly ListView files=new ListView(), onlineFiles=new ListView(); readonly Button back=new Button(), choose=new Button(), addFolder=new Button(), refresh=new Button();
 readonly ImageList onlineThumbs=new ImageList(); readonly Timer animation=new Timer(); readonly ToolTip tips=new ToolTip();
 System.Threading.CancellationTokenSource portraitScan=new System.Threading.CancellationTokenSource(), onlineScan=new System.Threading.CancellationTokenSource(), thumbnailScan=new System.Threading.CancellationTokenSource();
 bool fetchingPortraits=false, onlineMode=false, onlineSearching=false, thumbnailsLoading=false, thumbnailsPaused=false;
 List<Person> people=new List<Person>(); List<Video> videos=new List<Video>(); List<OnlineResult> onlineResults=new List<OnlineResult>();
 readonly HashSet<string> thumbnailAttempted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 string root="", onlineError="", onlineQueryFor=""; Person current; int generation=0, skipped=0; bool suppress=false; ListViewItem lastOnlineTipItem;
 OnlineSettings onlineSettings=OnlineSettings.Load();
 readonly string settings=AppDataPaths.MigrateFile("directory.txt");
 public Shelf() {
  XdolfTheme.ConfigureToolTip(tips);
  Text="VideoShelf"; Size=new Size(1120,780); MinimumSize=new Size(940,540); StartPosition=FormStartPosition.CenterScreen; BackColor=bg; ForeColor=XdolfTheme.Text; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; KeyPreview=true;
  var header=new Panel { Dock=DockStyle.Top,Height=156,Padding=new Padding(28),BackColor=XdolfTheme.Background };
  title.SetBounds(28,22,650,39); title.Font=new Font("Segoe UI",24,FontStyle.Bold); title.ForeColor=XdolfTheme.TextStrong; title.Text="";
  subtitle.SetBounds(30,69,1000,24); subtitle.ForeColor=muted; subtitle.Text="Choose a folder to get started"; subtitle.AutoEllipsis=true; subtitle.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  Style(choose,"Choose folder",28,111,135); Style(addFolder,"+ Add folder",173,111,105); Style(refresh,"Refresh",288,111,90); Style(back,"← People",388,111,105); back.Visible=false;
  tips.SetToolTip(choose,"Select the parent directory that VideoShelf should use as its library.");
  tips.SetToolTip(refresh,"Rescan the current view and refresh its local or online data.");
  tips.SetToolTip(back,"Return to the collection grid.");
  search.SetBounds(503,113,230,29); XdolfTheme.StyleInput(search); search.BorderStyle=BorderStyle.FixedSingle; tips.SetToolTip(search,"Filter the current people, local-video or online-result view. Shortcut: Ctrl+F"); search.AccessibleName="Filter";
  sort.SetBounds(748,111,160,30); sort.DropDownStyle=ComboBoxStyle.DropDownList; XdolfTheme.StyleInput(sort); tips.SetToolTip(sort,"Change the ordering used by the current view."); SetSort(false);
  addFolder.Enabled=false;tips.SetToolTip(addFolder,"Create a new folder or collection inside the selected VideoShelf library. Shortcut: Ctrl+N");
  header.Controls.AddRange(new Control[]{title,subtitle,choose,addFolder,refresh,back,search,sort}); SetHeader(false);
  status.Dock=DockStyle.Bottom; status.Height=38; status.Padding=new Padding(28,8,0,0); status.ForeColor=muted; status.BackColor=XdolfTheme.Background;
  content.Dock=DockStyle.Fill; content.Padding=new Padding(28,10,8,0); content.BackColor=XdolfTheme.Background; cards.Dock=DockStyle.Fill; cards.AutoScroll=true; cards.BackColor=XdolfTheme.Background; content.Controls.Add(cards);
  detail.Dock=DockStyle.Fill; detail.Visible=false; detail.BackColor=XdolfTheme.Background; content.Controls.Add(detail);

  localDetail.Dock=DockStyle.Fill; localDetail.BackColor=XdolfTheme.Background;
  var actions=new FlowLayoutPanel {Dock=DockStyle.Top,Height=49,WrapContents=false,BackColor=XdolfTheme.Background}; var play=new Button(); Style(play,"▶ Play selected",0,0,145); var folder=new Button(); Style(folder,"Open person folder",0,0,170); var findOnline=new Button(); Style(findOnline,"Find online",0,0,120); actions.Controls.AddRange(new Control[]{play,folder,findOnline});
  tips.SetToolTip(play,"Open the selected local video using the Windows default media player.");
  tips.SetToolTip(folder,"Open this collection's physical folder in Windows Explorer.");
  tips.SetToolTip(findOnline,"Search the configured online source for seeded matches to this collection.");
  files.Dock=DockStyle.Fill; files.View=View.Details; files.FullRowSelect=true; files.MultiSelect=false; files.HideSelection=false; files.BackColor=XdolfTheme.Panel; files.ForeColor=XdolfTheme.Text; files.BorderStyle=BorderStyle.FixedSingle;
  files.Columns.Add("VIDEO",400); files.Columns.Add("TYPE",80); files.Columns.Add("SIZE",100); files.Columns.Add("MODIFIED",150); files.Columns.Add("SUBFOLDER",200);
  localDetail.Controls.Add(files); localDetail.Controls.Add(actions);

  onlineDetail.Dock=DockStyle.Fill; onlineDetail.Visible=false; onlineDetail.BackColor=XdolfTheme.Background;
  var onlineActions=new FlowLayoutPanel {Dock=DockStyle.Top,Height=78,WrapContents=true,AutoScroll=false,BackColor=XdolfTheme.Background};
  var local=new Button(); Style(local,"← Local videos",0,0,125); onlineQuery.Width=390;onlineQuery.Height=29;XdolfTheme.StyleInput(onlineQuery);onlineQuery.BorderStyle=BorderStyle.FixedSingle;onlineQuery.Margin=new Padding(3,2,3,3);onlineQuery.AccessibleName="Online search";
  var searchOnline=new Button();Style(searchOnline,"Search online",0,0,125);var configure=new Button();Style(configure,"Online source",0,0,125);
  tips.SetToolTip(local,"Return to videos already stored in this collection."); tips.SetToolTip(onlineQuery,"Edit the query sent to the configured online source."); tips.SetToolTip(searchOnline,"Run the current online search query."); tips.SetToolTip(configure,"Configure the Torznab-compatible source used for online searches.");
  onlineActions.Controls.AddRange(new Control[]{local,onlineQuery,searchOnline,configure});onlineActions.SetFlowBreak(configure,true);
  var resLabel=new Label{Text="Resolution",ForeColor=muted,Width=75,Height=27,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(3,3,0,0)};
  resolution.Width=125;resolution.DropDownStyle=ComboBoxStyle.DropDownList;XdolfTheme.StyleInput(resolution);resolution.Items.AddRange(new object[]{"All resolutions","2160p","1080p","720p","Other"});resolution.SelectedIndex=0;tips.SetToolTip(resolution,"Filter online results by detected video resolution.");
  var openOnline=new Button();Style(openOnline,"Open selected",0,0,125);var copyLink=new Button();Style(copyLink,"Copy link",0,0,105);tips.SetToolTip(openOnline,"Open the selected online result with its registered Windows handler.");tips.SetToolTip(copyLink,"Copy the selected result link to the clipboard.");
  onlineActions.Controls.AddRange(new Control[]{resLabel,resolution,openOnline,copyLink});
  onlineFiles.Dock=DockStyle.Fill;onlineFiles.View=View.Details;onlineFiles.FullRowSelect=true;onlineFiles.MultiSelect=false;onlineFiles.HideSelection=false;onlineFiles.BackColor=XdolfTheme.Panel;onlineFiles.ForeColor=XdolfTheme.Text;onlineFiles.BorderStyle=BorderStyle.FixedSingle;onlineFiles.ShowItemToolTips=false;
  onlineThumbs.ColorDepth=ColorDepth.Depth32Bit;onlineThumbs.ImageSize=new Size(96,54);ResetOnlineThumbnailImages();onlineFiles.SmallImageList=onlineThumbs;
  onlineFiles.Columns.Add("ONLINE RESULT",455);onlineFiles.Columns.Add("RESOLUTION",90);onlineFiles.Columns.Add("SIZE",100);onlineFiles.Columns.Add("SEEDERS",75);onlineFiles.Columns.Add("LEECHERS",80);onlineFiles.Columns.Add("SOURCE",125);onlineFiles.Columns.Add("PUBLISHED",135);
  onlineFiles.MouseMove+=delegate(object sender,MouseEventArgs e){
   var item=onlineFiles.HitTest(e.Location).Item;
   if(item==lastOnlineTipItem)return;
   lastOnlineTipItem=item;tips.Hide(onlineFiles);
   if(item!=null&&!string.IsNullOrWhiteSpace(item.ToolTipText))tips.Show(item.ToolTipText,onlineFiles,e.X+14,e.Y+18,6000);
  };
  onlineFiles.MouseLeave+=delegate{lastOnlineTipItem=null;tips.Hide(onlineFiles);};
  onlineDetail.Controls.Add(onlineFiles);onlineDetail.Controls.Add(onlineActions);
  detail.Controls.Add(localDetail);detail.Controls.Add(onlineDetail);

  Controls.Add(content); Controls.Add(status); Controls.Add(header);
  choose.Click+=delegate { using(var d=new FolderBrowserDialog {Description="Choose the parent folder containing one folder per person",SelectedPath=root,ShowNewFolderButton=false}) if(d.ShowDialog(this)==DialogResult.OK) LoadRoot(d.SelectedPath); };
  addFolder.Click+=delegate {AddLibraryFolder();};
  refresh.Click+=delegate {if(root.Length>0) {if(onlineMode&&current!=null)SearchOnline(onlineQuery.Text,true);else if(current!=null)OpenPerson(current);else LoadRoot(root);} };
  back.Click+=delegate {ShowPeople();}; search.TextChanged+=delegate {if(!suppress) Render();}; sort.SelectedIndexChanged+=delegate {if(!suppress)Render();};
  play.Click+=delegate {Play();}; files.DoubleClick+=delegate {Play();}; files.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){Play();e.Handled=true;}};
  folder.Click+=delegate {if(current!=null) Launch(current.Path);};findOnline.Click+=delegate{ShowOnline();};local.Click+=delegate{ShowLocal();};
  searchOnline.Click+=delegate{SearchOnline(onlineQuery.Text,true);};onlineQuery.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){SearchOnline(onlineQuery.Text,true);e.SuppressKeyPress=true;}};
  configure.Click+=delegate{ConfigureOnline();};resolution.SelectedIndexChanged+=delegate{if(onlineMode)RenderOnline();};openOnline.Click+=delegate{OpenOnline();};copyLink.Click+=delegate{CopyOnline();};onlineFiles.DoubleClick+=delegate{OpenOnline();};onlineFiles.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){OpenOnline();e.Handled=true;}};
  KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.F){search.Focus();e.SuppressKeyPress=true;} if(e.Control&&e.KeyCode==Keys.N&&current==null){AddLibraryFolder();e.SuppressKeyPress=true;} if(e.KeyCode==Keys.Escape&&current!=null){if(onlineMode)ShowLocal();else ShowPeople();} if(e.KeyCode==Keys.F5) refresh.PerformClick();};
  animation.Interval=15; animation.Tick+=delegate { files.Top=Math.Max(49,files.Top-12); if(files.Top<=49){animation.Stop();files.Dock=DockStyle.Fill;} };
  Shown+=delegate {try {if(File.Exists(settings)&&Directory.Exists(File.ReadAllText(settings))) LoadRoot(File.ReadAllText(settings));else status.Text="Choose a folder • portraits found by folder name • videos open in your default player";}catch{status.Text="Choose a folder to begin.";} };
  FormClosed+=delegate {generation++;portraitScan.Cancel();onlineScan.Cancel();thumbnailScan.Cancel();portraitScan.Dispose();onlineScan.Dispose();thumbnailScan.Dispose();onlineThumbs.Dispose();animation.Dispose();tips.Dispose();foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();};
 }
 void SetHeader(bool showName){
  Control header=title.Parent; int height=showName?156:104; int shift=height-header.Height;
  foreach(Control control in header.Controls)if(control!=title)control.Top+=shift;
  header.Height=height;title.Visible=showName;addFolder.Visible=!showName;addFolder.Enabled=!showName&&root.Length>0&&Directory.Exists(root);
 }
 void SetSort(bool online){
  suppress=true;sort.Items.Clear();if(online)sort.Items.AddRange(new object[]{"Seeders high–low","Name A–Z","Size high–low"});else sort.Items.AddRange(new object[]{"Name A–Z","Name Z–A","Newest first"});sort.SelectedIndex=0;suppress=false;
 }
 void Style(Button b,string t,int x,int y,int w){b.Text=t;b.SetBounds(x,y,w,32);XdolfTheme.StyleButton(b);b.Cursor=Cursors.Hand;}
 Image MakeOnlinePlaceholder(){var bmp=new Bitmap(96,54);using(Graphics g=Graphics.FromImage(bmp)){g.Clear(XdolfTheme.PanelRaised);using(Pen p=new Pen(XdolfTheme.Outline,2)){g.DrawRectangle(p,1,1,93,51);g.DrawLine(p,8,43,32,22);g.DrawLine(p,32,22,46,35);g.DrawLine(p,46,35,62,18);g.DrawLine(p,62,18,87,43);}}return bmp;}
 void ClearSearch(){suppress=true;search.Clear();suppress=false;}
 void ClearCards(){while(cards.Controls.Count>0)cards.Controls[0].Dispose();}
}
}
