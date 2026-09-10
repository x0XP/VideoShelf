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
 }

 void ApplyBrandIcon(){
  if(brandIconApplied||IsDisposed)return;
  brandIconApplied=true;

  var oldLogo=sidebar.Controls.OfType<LogoMark>().FirstOrDefault();
  if(oldLogo!=null){
   oldLogo.Visible=false;
   var logo=BrandPicture(oldLogo.Left,oldLogo.Top,oldLogo.Width,oldLogo.Height);
   sidebar.Controls.Add(logo);logo.BringToFront();
  }

  var titleBar=Controls.OfType<Panel>().FirstOrDefault(p=>p.Dock==DockStyle.Top&&p.Height<=36);
  if(titleBar!=null){
   var oldGlyph=titleBar.Controls.OfType<Label>().FirstOrDefault(l=>l.Text=="◈");
   if(oldGlyph!=null){
    oldGlyph.Visible=false;
    var icon=BrandPicture(8,4,26,26);
    titleBar.Controls.Add(icon);icon.BringToFront();
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
