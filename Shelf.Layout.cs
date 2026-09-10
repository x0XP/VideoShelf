using System;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 protected override void OnShown(EventArgs e){
  base.OnShown(e);
  LayoutMockupShell();
 }
 protected override void OnResize(EventArgs e){
  base.OnResize(e);
  if(IsHandleCreated)LayoutMockupShell();
 }
 void LayoutMockupShell(){
  if(IsDisposed||ClientSize.Width<=0||ClientSize.Height<=0)return;

  body.Dock=DockStyle.None;
  body.SetBounds(1,34,Math.Max(0,ClientSize.Width-2),Math.Max(0,ClientSize.Height-63));
  body.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  body.SendToBack();

  Control results=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&c.Controls.OfType<FlowLayoutPanel>().Any(f=>f==onlineCards));
  Control filters=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c is Panel&&c.Controls.Contains(onlineQuery));
  int w=Math.Max(0,searchView.ClientSize.Width),h=Math.Max(0,searchView.ClientSize.Height),filterHeight=68,rightWidth=Math.Min(332,Math.Max(280,w/3));

  if(filters!=null){filters.Dock=DockStyle.None;filters.SetBounds(0,0,w,filterHeight);filters.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;filters.BringToFront();}
  inspector.Dock=DockStyle.None;
  inspector.SetBounds(Math.Max(0,w-rightWidth),filterHeight,rightWidth,Math.Max(0,h-filterHeight));
  inspector.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right;
  inspector.BringToFront();
  if(results!=null){results.Dock=DockStyle.None;results.SetBounds(0,filterHeight,Math.Max(0,w-rightWidth),Math.Max(0,h-filterHeight));results.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;results.SendToBack();}

  LayoutInspector();
  body.PerformLayout();mainHost.PerformLayout();searchView.PerformLayout();
  ResizeOnlineCards();
 }
 void LayoutInspector(){
  int w=inspector.ClientSize.Width,h=inspector.ClientSize.Height,pad=13,inner=Math.Max(0,w-pad*2);
  inspectorImage.SetBounds(pad,14,inner,205);
  inspectorTitle.SetBounds(pad,233,inner,58);
  inspectorTags.SetBounds(pad,300,inner,58);
  int downloadY=Math.Max(500,h-257),streamY=Math.Max(downloadY+72,h-185),bottomY=Math.Max(streamY+72,h-107);
  inspectorMeta.SetBounds(pad,369,inner,Math.Max(90,downloadY-389));
  downloadOnline.SetBounds(pad,downloadY,inner,62);
  streamOnline.SetBounds(pad,streamY,inner,62);
  int half=Math.Max(90,(inner-12)/2);
  copyLink.SetBounds(pad,bottomY,half,47);
  viewFiles.SetBounds(pad+half+12,bottomY,Math.Max(90,inner-half-12),47);
 }
}
}
