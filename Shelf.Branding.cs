using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool brandIconApplied;

 protected override void OnHandleCreated(EventArgs e){
  base.OnHandleCreated(e);
  try{Icon=(Icon)AppBrand.Icon.Clone();}catch{}
  ApplyBrandIcon();
  EnsureHomeDashboard();
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
   var logo=BrandPicture(left,top,width,height);
   sidebar.Controls.Add(logo);logo.BringToFront();
  }

  // Reuse the existing draggable title-bar label instead of overlaying a new
  // PictureBox on top of it. Keeping the original control also preserves its
  // mouse handlers and hit-testing.
  var titleBar=Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Top&&p.Height<=36);
  if(titleBar!=null){
   var glyph=titleBar.Controls.OfType<Label>().FirstOrDefault(l=>l.Text=="◈");
   if(glyph!=null){
    Image image=AppBrand.Bitmap(glyph.Width,glyph.Height);
    glyph.Text="";
    glyph.Image=image;
    glyph.ImageAlign=ContentAlignment.MiddleCenter;
    glyph.Disposed+=delegate{try{image.Dispose();}catch{}};
   }
  }
 }

 PictureBox BrandPicture(int left,int top,int width,int height){
  Image image=AppBrand.Bitmap(width,height);
  var box=new PictureBox{Left=left,Top=top,Width=width,Height=height,BackColor=Color.Transparent,SizeMode=PictureBoxSizeMode.Zoom,Image=image,TabStop=false};
  box.Disposed+=delegate{if(image!=null)image.Dispose();};
  return box;
 }
}
}