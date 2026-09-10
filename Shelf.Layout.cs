using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 MockupActionButton downloadVisual,streamVisual;

 protected override void OnShown(EventArgs e){
  base.OnShown(e);
  ApplyExtendedVisuals();
  EnsureMockupActions();
  LayoutMockupShell();
  ApplyHomePolish();
  LayoutHomePolish();
  EnsureHomeDashboard();
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

  navHome.Top=116;navSearch.Top=170;navCollections.Top=224;navDownloads.Top=278;navStreaming.Top=332;navSettings.Top=386;
  LayoutSearchSurface();
  if(homePolishApplied)LayoutHomePolish();
  if(homeDashboardReady)LayoutDashboardHome();
  if(finalPolishApplied)LayoutFinalPolish();
  body.PerformLayout();mainHost.PerformLayout();searchView.PerformLayout();
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
  int margin=17,gap=10,buttonW=92,sourceW=width<1000?112:130,categoryW=width<1000?100:112,resW=width<1000?112:126;
  int fixedWidth=margin*2+gap*4+buttonW+sourceW+categoryW+resW;
  int queryW=Math.Max(250,Math.Min(480,width-fixedWidth));
  int x=margin;
  if(queryFrame!=null){queryFrame.SetBounds(x,14,queryW,38);onlineQuery.SetBounds(12,9,Math.Max(80,queryW-60),22);if(queryGlyph!=null)queryGlyph.SetBounds(queryW-40,5,32,28);}x+=queryW+gap;
  sourceFilter.SetBounds(x,13,sourceW,40);x+=sourceW+gap;
  categoryFilter.SetBounds(x,13,categoryW,40);x+=categoryW+gap;
  resolution.SetBounds(x,13,resW,40);x+=resW+gap;
  searchOnline.SetBounds(x,13,Math.Max(78,Math.Min(buttonW,width-margin-x)),40);
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
  int imageHeight=h<660?160:h<760?185:205;
  inspectorImage.SetBounds(pad,14,inner,imageHeight);
  int titleTop=14+imageHeight+14;
  inspectorTitle.SetBounds(pad,titleTop,inner,64);
  inspectorTags.SetBounds(pad,titleTop+74,inner,36);
  int metaTop=titleTop+123;
  int downloadY=Math.Max(metaTop+115,h-245),streamY=Math.Max(downloadY+72,h-173),bottomY=Math.Max(streamY+72,h-95);
  inspectorMeta.SetBounds(pad,metaTop,inner,Math.Max(90,downloadY-metaTop-18));
  downloadVisual.SetBounds(pad,downloadY,inner,62);
  streamVisual.SetBounds(pad,streamY,inner,62);
  int half=Math.Max(90,(inner-12)/2);
  copyLink.SetBounds(pad,bottomY,half,47);
  viewFiles.SetBounds(pad+half+12,bottomY,Math.Max(90,inner-half-12),47);
  downloadVisual.Enabled=downloadOnline.Enabled;streamVisual.Enabled=streamOnline.Enabled;
  if(finalPolishApplied)LayoutFinalPolish();
 }
}
}
