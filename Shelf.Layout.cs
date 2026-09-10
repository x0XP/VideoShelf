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
 }
 protected override void OnResize(EventArgs e){
  base.OnResize(e);
  if(IsHandleCreated)LayoutMockupShell();
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
  body.PerformLayout();mainHost.PerformLayout();searchView.PerformLayout();
 }
 void LayoutSearchSurface(){
  ApplyExtendedVisuals();EnsureMockupActions();
  Control results=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&c.Controls.OfType<FlowLayoutPanel>().Any(f=>f==onlineCards));
  Control filters=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&c.Contains(onlineQuery));
  int w=Math.Max(0,searchView.ClientSize.Width),h=Math.Max(0,searchView.ClientSize.Height),filterHeight=68,rightWidth=Math.Min(332,Math.Max(280,w/3)),rightMargin=12,gap=12;
  int inspectorX=Math.Max(0,w-rightMargin-rightWidth),contentWidth=Math.Max(0,inspectorX-gap),contentHeight=Math.Max(0,h-filterHeight-12);

  configure.Visible=false;
  if(filters!=null){filters.Dock=DockStyle.None;filters.SetBounds(0,0,w,filterHeight);filters.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;filters.BringToFront();}
  inspector.Dock=DockStyle.None;
  inspector.SetBounds(inspectorX,filterHeight,rightWidth,contentHeight);
  inspector.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right;
  inspector.BringToFront();
  if(results!=null){results.Dock=DockStyle.None;results.SetBounds(0,filterHeight,contentWidth,contentHeight);results.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left;results.SendToBack();}

  onlineCards.Padding=new Padding(22,0,10,16);LayoutExtendedVisuals();
  LayoutInspector();searchView.PerformLayout();ResizeOnlineCards();
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
  return homeView;
 }
 void LayoutInspector(){
  int w=inspector.ClientSize.Width,h=inspector.ClientSize.Height,pad=13,inner=Math.Max(0,w-pad*2);
  inspectorImage.SetBounds(pad,14,inner,205);
  inspectorTitle.SetBounds(pad,233,inner,54);
  inspectorTags.SetBounds(pad,296,inner,36);
  int downloadY=Math.Max(475,h-245),streamY=Math.Max(downloadY+72,h-173),bottomY=Math.Max(streamY+72,h-95);
  inspectorMeta.SetBounds(pad,345,inner,Math.Max(105,downloadY-363));
  downloadVisual.SetBounds(pad,downloadY,inner,62);
  streamVisual.SetBounds(pad,streamY,inner,62);
  int half=Math.Max(90,(inner-12)/2);
  copyLink.SetBounds(pad,bottomY,half,47);
  viewFiles.SetBounds(pad+half+12,bottomY,Math.Max(90,inner-half-12),47);
  downloadVisual.Enabled=downloadOnline.Enabled;streamVisual.Enabled=streamOnline.Enabled;
 }
}
}
