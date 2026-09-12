using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool brandIconApplied;
 PictureBox sidebarBrand;
 Label titleBrand;

 protected override void OnHandleCreated(EventArgs e){
  base.OnHandleCreated(e);
  RefreshWindowIcon();
  ApplyBrandIcon();
  EnsureHomeDashboard();
 }

 protected override void OnDpiChanged(DpiChangedEventArgs e){
  base.OnDpiChanged(e);
  RefreshWindowIcon();
  RefreshBrandImages();
 }

 void RefreshWindowIcon(){
  try{
   using(var dpiIcon=AppBrand.IconForDpi(DeviceDpi))Icon=(Icon)dpiIcon.Clone();
  }catch{try{Icon=(Icon)AppBrand.Icon.Clone();}catch{}}
 }

 void ApplyBrandIcon(){
  if(brandIconApplied||IsDisposed)return;
  brandIconApplied=true;

  // Replace the legacy sidebar mark rather than hiding it underneath another
  // control. This keeps one visual/control in the layout and avoids duplicate
  // controls fighting DPI/layout passes.
  var oldLogo=sidebar.Controls.OfType<LogoMark>().FirstOrDefault();
  if(oldLogo!=null){
   int left=oldLogo.Left,top=oldLogo.Top,width=oldLogo.Width,height=oldLogo.Height;
   sidebar.Controls.Remove(oldLogo);
   oldLogo.Dispose();
   sidebarBrand=BrandPicture(left,top,width,height);
   sidebar.Controls.Add(sidebarBrand);sidebarBrand.BringToFront();
  }

  // Reuse the existing draggable title-bar label instead of overlaying a new
  // PictureBox on top of it. Keeping the original control also preserves its
  // mouse handlers and hit-testing.
  var titleBar=Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Top&&p.Height<=36);
  if(titleBar!=null){
   titleBrand=titleBar.Controls.OfType<Label>().FirstOrDefault(l=>l.Text=="◈");
   if(titleBrand!=null){
    titleBrand.Text="";
    titleBrand.ImageAlign=ContentAlignment.MiddleCenter;
    ReplaceTitleImage();
    Label label=titleBrand;
    label.Disposed+=delegate{
     Image old=label.Image;label.Image=null;
     if(old!=null)try{old.Dispose();}catch{}
    };
   }
  }
 }

 void RefreshBrandImages(){
  if(sidebarBrand!=null&&!sidebarBrand.IsDisposed){
   Image old=sidebarBrand.Image;
   sidebarBrand.Image=AppBrand.Bitmap(sidebarBrand.Width,sidebarBrand.Height);
   if(old!=null)try{old.Dispose();}catch{}
  }
  ReplaceTitleImage();
 }

 void ReplaceTitleImage(){
  if(titleBrand==null||titleBrand.IsDisposed)return;
  Image old=titleBrand.Image;
  titleBrand.Image=AppBrand.Bitmap(titleBrand.Width,titleBrand.Height);
  if(old!=null)try{old.Dispose();}catch{}
 }

 PictureBox BrandPicture(int left,int top,int width,int height){
  var box=new PictureBox{Left=left,Top=top,Width=width,Height=height,BackColor=Color.Transparent,SizeMode=PictureBoxSizeMode.Zoom,Image=AppBrand.Bitmap(width,height),TabStop=false};
  box.Disposed+=delegate{
   Image old=box.Image;box.Image=null;
   if(old!=null)try{old.Dispose();}catch{}
  };
  return box;
 }
}
}