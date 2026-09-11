using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 MockupActionButton downloadVisual,streamVisual;
 Button chromeMin,chromeMax,chromeClose;
 Label collectionEmptyCue;
 Panel shellStatusBar;
 bool shellChromePrepared,inspectorPlaceholderPaintHooked,libraryCueApplied,playButtonPaintHooked;

 protected override void OnShown(EventArgs e){
  base.OnShown(e);
  ApplyExtendedVisuals();
  EnsureMockupActions();
  LayoutMockupShell();
  ApplyHomePolish();
  LayoutHomePolish();
  EnsureHomeDashboard();
  EnsureShellAuxVisuals();
  RefreshCollectionVisualState();
  if(section==ShellSection.Home)ShowDashboardHome();
 }
 protected override void OnResize(EventArgs e){
  base.OnResize(e);
  if(IsHandleCreated){LayoutMockupShell();if(homePolishApplied)LayoutHomePolish();if(homeDashboardReady)LayoutDashboardHome();}
 }
 void EnsureMockupActions(){
  if(downloadVisual!=null&&streamVisual!=null)return;
  downloadVisual=new MockupActionButton{TitleText="Download locally",SubtitleText="Save to your device",Glyph="\uE896",Accent=XdolfTheme.AccentBlue};
  streamVisual=new MockupActionButton{TitleText="Stream locally",SubtitleText="Play without downloading",Glyph="\uE768",Accent=XdolfTheme.AccentRed};
  downloadVisual.Click+=delegate{if(downloadOnline.Enabled)downloadOnline.PerformClick();};
  streamVisual.Click+=delegate{if(streamOnline.Enabled)streamOnline.PerformClick();};
  downloadOnline.EnabledChanged+=delegate{downloadVisual.Enabled=downloadOnline.Enabled;};
  streamOnline.EnabledChanged+=delegate{streamVisual.Enabled=streamOnline.Enabled;};
  downloadVisual.Enabled=downloadOnline.Enabled;streamVisual.Enabled=streamOnline.Enabled;
  tips.SetToolTip(downloadVisual,"Download locally\nSave the selected torrent permanently to a folder you choose.");
  tips.SetToolTip(streamVisual,"Stream locally\nStart the selected torrent in VideoShelf's local streaming player.");
  inspector.Controls.Add(downloadVisual);inspector.Controls.Add(streamVisual);
  downloadOnline.Visible=false;streamOnline.Visible=false;
  downloadVisual.BringToFront();streamVisual.BringToFront();
 }
 void LayoutMockupShell(){
  if(IsDisposed||ClientSize.Width<=0||ClientSize.Height<=0)return;
  ApplyExtendedVisuals();EnsureMockupActions();

  body.Dock=DockStyle.None;
  body.SetBounds(1,34,Math.Max(0,ClientSize.Width-2),Math.Max(0,ClientSize.Height-63));
  body.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  body.SendToBack();

  EnsureShellChrome();
  EnsureShellAuxVisuals();
  navHome.Top=116;navSearch.Top=170;navCollections.Top=224;navDownloads.Top=278;navStreaming.Top=332;navSettings.Top=386;
  LayoutSearchSurface();
  if(homePolishApplied)LayoutHomePolish();
  if(homeDashboardReady)LayoutDashboardHome();
  if(finalPolishApplied)LayoutFinalPolish();
  LayoutCollectionAuxVisuals();
  body.PerformLayout();mainHost.PerformLayout();searchView.PerformLayout();
 }
 void EnsureShellChrome(){
  int navWidth=Math.Max(160,sidebar.ClientSize.Width-4);
  foreach(var n in new[]{navHome,navSearch,navCollections,navDownloads,navStreaming,navSettings}){
   n.Left=0;n.Width=navWidth;n.Anchor=AnchorStyles.Top|AnchorStyles.Left;
  }
  sidebar.Invalidate();

  if(!shellChromePrepared){
   Panel bar=Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Top&&p.Height==34);
   if(bar!=null){
    chromeMin=bar.Controls.OfType<Button>().FirstOrDefault(b=>b.Text=="—");
    chromeMax=bar.Controls.OfType<Button>().FirstOrDefault(b=>b.Text=="□");
    chromeClose=bar.Controls.OfType<Button>().FirstOrDefault(b=>b.Text=="×");
    if(chromeMin!=null&&chromeMax!=null&&chromeClose!=null){
     PrepareCaptionButton(chromeMin);PrepareCaptionButton(chromeMax);PrepareCaptionButton(chromeClose);
     chromeMin.Paint+=PaintMinimizeGlyph;chromeMax.Paint+=PaintMaximizeGlyph;chromeClose.Paint+=PaintCloseGlyph;
     bar.Resize+=delegate{LayoutCaptionButtons();};
     shellChromePrepared=true;
    }
   }
  }
  LayoutCaptionButtons();
 }
 void EnsureShellAuxVisuals(){
  if(shellStatusBar==null){
   shellStatusBar=Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Bottom&&p.Height>=28&&p.Height<=30);
   if(shellStatusBar!=null){
    statusLeft.AutoEllipsis=true;statusRight.AutoEllipsis=true;
    statusLeft.Anchor=AnchorStyles.Top|AnchorStyles.Left;statusRight.Anchor=AnchorStyles.Top|AnchorStyles.Left;
    shellStatusBar.Resize+=delegate{LayoutStatusBarVisuals();};
   }
  }
  ApplyLibraryFilterCue();
  if(collectionEmptyCue==null){
   collectionEmptyCue=new Label{TextAlign=ContentAlignment.MiddleCenter,BackColor=files.BackColor,ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",10.5f),BorderStyle=BorderStyle.FixedSingle,Visible=false};
   collectionView.Controls.Add(collectionEmptyCue);collectionEmptyCue.BringToFront();
   collectionView.VisibleChanged+=delegate{RefreshCollectionVisualState();};
   files.SelectedIndexChanged+=delegate{RefreshCollectionVisualState();};
   files.Resize+=delegate{RefreshCollectionVisualState();};
   statusLeft.TextChanged+=delegate{if(collectionView.Visible)RefreshCollectionVisualState();};
  }
  if(!inspectorPlaceholderPaintHooked){inspectorImage.Paint+=PaintInspectorArtworkPlaceholder;inspectorPlaceholderPaintHooked=true;}
  if(!playButtonPaintHooked){playLocal.Paint+=PaintDisabledPlayButton;playButtonPaintHooked=true;}
  LayoutStatusBarVisuals();LayoutCollectionAuxVisuals();RefreshCollectionVisualState();
 }
 void ApplyLibraryFilterCue(){
  if(libraryCueApplied)return;
  libraryFilter.HandleCreated+=delegate{SetCueBanner();};
  if(libraryFilter.IsHandleCreated)SetCueBanner();
 }
 void SetCueBanner(){
  if(libraryFilter.IsDisposed||!libraryFilter.IsHandleCreated)return;
  try{SendMessage(libraryFilter.Handle,0x1501,IntPtr.Zero,"Filter collections");libraryCueApplied=true;}catch{}
 }
 void LayoutStatusBarVisuals(){
  if(shellStatusBar==null||shellStatusBar.IsDisposed)return;
  int w=shellStatusBar.ClientSize.Width,leftWidth=Math.Min(500,Math.Max(180,w/3));
  statusLeft.SetBounds(18,6,leftWidth,18);
  int rightX=leftWidth+36;statusRight.SetBounds(rightX,6,Math.Max(0,w-rightX-18),18);
 }
 void LayoutCollectionAuxVisuals(){
  if(collectionEmptyCue!=null){
   collectionEmptyCue.Bounds=files.Bounds;
   if(collectionEmptyCue.Visible)collectionEmptyCue.BringToFront();
  }
 }
 internal void RefreshCollectionVisualState(){
  if(files==null||files.IsDisposed)return;
  bool empty=files.Items.Count==0;
  playLocal.Enabled=!empty&&files.SelectedItems.Count>0;
  int visibleRows=Math.Max(1,(Math.Max(0,files.ClientSize.Height-25))/22);
  if(!empty)files.Scrollable=files.Items.Count>visibleRows;
  files.Visible=!empty;
  if(files.Visible)NativeDarkScroll.Apply(files);
  if(collectionEmptyCue!=null){
   bool scanning=statusLeft.Text.StartsWith("Scanning collection",StringComparison.OrdinalIgnoreCase);
   collectionEmptyCue.Text=scanning?"Scanning local videos…":"No local videos in this collection.\r\nUse Find online to search seeded metadata.";
   collectionEmptyCue.Visible=empty&&collectionView.Visible;
   LayoutCollectionAuxVisuals();
  }
 }
 void PaintInspectorArtworkPlaceholder(object sender,PaintEventArgs e){
  if(inspectorImage.Image!=null||inspectorImage.ClientSize.Width<40||inspectorImage.ClientSize.Height<40)return;
  int w=inspectorImage.ClientSize.Width,h=inspectorImage.ClientSize.Height,cx=w/2,cy=h/2-8;
  using(var p=new Pen(Color.FromArgb(48,72,92),2f)){
   e.Graphics.DrawLine(p,cx-48,cy+24,cx-18,cy-8);
   e.Graphics.DrawLine(p,cx-18,cy-8,cx+5,cy+13);
   e.Graphics.DrawLine(p,cx+5,cy+13,cx+48,cy-20);
  }
  using(var f=new Font("Segoe UI",8.5f))TextRenderer.DrawText(e.Graphics,"Artwork preview",f,new Rectangle(0,cy+38,w,20),Color.FromArgb(111,137,160),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
 }
 void PaintDisabledPlayButton(object sender,PaintEventArgs e){
  if(playLocal.Enabled)return;
  Rectangle r=new Rectangle(0,0,Math.Max(1,playLocal.ClientSize.Width-1),Math.Max(1,playLocal.ClientSize.Height-1));
  using(var b=new SolidBrush(Color.FromArgb(10,20,29)))e.Graphics.FillRectangle(b,r);
  using(var p=new Pen(Color.FromArgb(31,49,63)))e.Graphics.DrawRectangle(p,r);
  TextRenderer.DrawText(e.Graphics,playLocal.Text,playLocal.Font,r,Color.FromArgb(112,133,153),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
 }
 void PrepareCaptionButton(Button button){
  button.Text="";button.TabStop=false;button.AutoSize=false;button.Width=46;button.Height=34;
  button.Anchor=AnchorStyles.Top|AnchorStyles.Right;button.FlatStyle=FlatStyle.Flat;button.UseVisualStyleBackColor=false;
  button.FlatAppearance.BorderSize=0;button.Margin=Padding.Empty;button.Padding=Padding.Empty;
 }
 void LayoutCaptionButtons(){
  if(chromeMin==null||chromeMax==null||chromeClose==null||chromeClose.Parent==null)return;
  int right=chromeClose.Parent.ClientSize.Width,bw=46;
  chromeClose.SetBounds(right-bw,0,bw,34);
  chromeMax.SetBounds(right-bw*2,0,bw,34);
  chromeMin.SetBounds(right-bw*3,0,bw,34);
  chromeMin.Invalidate();chromeMax.Invalidate();chromeClose.Invalidate();
 }
 void PaintMinimizeGlyph(object sender,PaintEventArgs e){
  Button b=(Button)sender;int cx=b.ClientSize.Width/2,cy=b.ClientSize.Height/2;
  using(var p=new Pen(Color.FromArgb(224,232,240),1f))e.Graphics.DrawLine(p,cx-5,cy+2,cx+5,cy+2);
 }
 void PaintMaximizeGlyph(object sender,PaintEventArgs e){
  Button b=(Button)sender;int cx=b.ClientSize.Width/2,cy=b.ClientSize.Height/2;
  using(var p=new Pen(Color.FromArgb(224,232,240),1f)){
   if(WindowState==FormWindowState.Maximized){e.Graphics.DrawRectangle(p,cx-3,cy-5,8,8);e.Graphics.DrawRectangle(p,cx-5,cy-3,8,8);}
   else e.Graphics.DrawRectangle(p,cx-5,cy-5,10,10);
  }
 }
 void PaintCloseGlyph(object sender,PaintEventArgs e){
  Button b=(Button)sender;int cx=b.ClientSize.Width/2,cy=b.ClientSize.Height/2;
  using(var p=new Pen(Color.FromArgb(232,238,244),1.2f)){
   e.Graphics.DrawLine(p,cx-5,cy-5,cx+5,cy+5);e.Graphics.DrawLine(p,cx+5,cy-5,cx-5,cy+5);
  }
 }
 void LayoutSearchSurface(){
  ApplyExtendedVisuals();EnsureMockupActions();
  Control results=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&c.Controls.OfType<FlowLayoutPanel>().Any(f=>f==onlineCards));
  Control filters=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&(c.Contains(queryFrame)||c.Contains(onlineQuery)));
  int w=Math.Max(0,searchView.ClientSize.Width),h=Math.Max(0,searchView.ClientSize.Height),filterHeight=68;
  int rightWidth=w<980?280:w<1180?305:332,rightMargin=12,gap=12;
  int inspectorX=Math.Max(0,w-rightMargin-rightWidth),contentWidth=Math.Max(0,inspectorX-gap),contentHeight=Math.Max(0,h-filterHeight-12);

  configure.Visible=false;
  if(filters!=null){filters.Dock=DockStyle.None;filters.SetBounds(0,0,w,filterHeight);filters.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;filters.BringToFront();LayoutSearchToolbar(w);}
  inspector.Dock=DockStyle.None;
  inspector.SetBounds(inspectorX,filterHeight,rightWidth,contentHeight);
  inspector.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right;
  inspector.BringToFront();
  if(results!=null){
   results.Dock=DockStyle.None;results.SetBounds(0,filterHeight,contentWidth,contentHeight);results.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left;results.SendToBack();
   var head=results.Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Top||p.Height==78);
   if(head!=null){
    head.Width=contentWidth;
    var note=head.Controls.OfType<Label>().FirstOrDefault(l=>l.Text.StartsWith("No media is downloaded",StringComparison.OrdinalIgnoreCase));
    if(note!=null){note.Visible=contentWidth>=880;if(note.Visible){note.SetBounds(Math.Max(470,contentWidth-300),15,280,42);note.BringToFront();}}
    searchHeading.Width=Math.Max(250,contentWidth-(note!=null&&note.Visible?330:40));
    searchCount.Width=Math.Max(250,contentWidth-40);
   }
  }

  onlineCards.Padding=new Padding(22,0,10,16);LayoutExtendedVisuals();
  LayoutInspector();if(finalPolishApplied)LayoutFinalPolish();searchView.PerformLayout();ResizeOnlineCards();
 }
 void LayoutSearchToolbar(int width){
  int compact=width<1000?1:0;
  int margin=compact==1?12:17,gap=compact==1?6:9,buttonW=compact==1?80:88,sourceW=compact==1?96:118,categoryW=compact==1?105:122,resW=compact==1?98:112,languageW=compact==1?110:124;
  int fixedWidth=margin*2+gap*5+buttonW+sourceW+categoryW+resW+languageW;
  int queryW=Math.Max(210,Math.Min(460,width-fixedWidth));
  int x=margin;
  if(queryFrame!=null){queryFrame.SetBounds(x,14,queryW,38);onlineQuery.SetBounds(12,9,Math.Max(80,queryW-60),22);if(queryGlyph!=null)queryGlyph.SetBounds(queryW-40,5,32,28);}x+=queryW+gap;
  sourceFilter.SetBounds(x,13,sourceW,40);x+=sourceW+gap;
  categoryFilter.SetBounds(x,13,categoryW,40);x+=categoryW+gap;
  resolution.SetBounds(x,13,resW,40);x+=resW+gap;
  languageFilter.SetBounds(x,13,languageW,40);x+=languageW+gap;
  searchOnline.SetBounds(x,13,Math.Max(72,Math.Min(buttonW,width-margin-x)),40);
  if(finalPolishApplied)LayoutFinalPolish();
 }
 internal void LayoutSearchForCapture(int width,int height){
  searchView.Dock=DockStyle.None;searchView.SetBounds(0,0,width,height);searchView.Anchor=AnchorStyles.Top|AnchorStyles.Left;LayoutSearchSurface();searchView.PerformLayout();
 }
 internal Control ActiveViewForCapture(){
  if(section==ShellSection.Search)return searchView;
  if(section==ShellSection.Downloads)return downloadsView;
  if(section==ShellSection.Streaming)return streamingView;
  if(section==ShellSection.Settings)return settingsView;
  if(collectionView.Visible)return collectionView;
  if(collectionsBrowserView.Visible)return collectionsBrowserView;
  return homeView;
 }
 internal Control SidebarForCapture(){return sidebar;}
 internal bool IsSearchViewForCapture(Control control){return control==searchView;}
 void LayoutInspector(){
  int w=inspector.ClientSize.Width,h=inspector.ClientSize.Height,pad=13,inner=Math.Max(0,w-pad*2);
  int imageHeight=h<620?90:h<680?120:h<760?185:205;
  inspectorImage.SetBounds(pad,14,inner,imageHeight);
  int titleTop=14+imageHeight+14;
  inspectorTitle.SetBounds(pad,titleTop,inner,64);
  inspectorTags.SetBounds(pad,titleTop+74,inner,58);
  int metaTop=titleTop+144;
  int bottomY=Math.Max(metaTop+255,h-54);
  int streamY=bottomY-70,downloadY=streamY-70;
  if(downloadY<metaTop+90){downloadY=metaTop+90;streamY=downloadY+70;bottomY=streamY+70;}
  inspectorMeta.SetBounds(pad,metaTop,inner,Math.Max(90,downloadY-metaTop-18));
  downloadVisual.SetBounds(pad,downloadY,inner,62);
  streamVisual.SetBounds(pad,streamY,inner,62);
  int half=Math.Max(90,(inner-12)/2);
  copyLink.SetBounds(pad,bottomY,half,47);
  viewFiles.SetBounds(pad+half+12,bottomY,Math.Max(90,inner-half-12),47);
  downloadVisual.Enabled=downloadOnline.Enabled;streamVisual.Enabled=streamOnline.Enabled;
  if(finalPolishApplied)LayoutFinalPolish();
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,string lParam);
}
}
