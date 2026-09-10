using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VideoShelf {
static class AppBrand {
 static Icon cached;
 public static Icon Icon {
  get {
   if(cached!=null)return cached;
   try{cached=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   if(cached==null){try{string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"VideoShelf.ico");if(File.Exists(path))cached=new Icon(path);}catch{}}
   if(cached==null)cached=SystemIcons.Application;
   return cached;
  }
 }
 public static Bitmap Bitmap(int width,int height){
  using(var source=Icon.ToBitmap())return new Bitmap(source,new Size(width,height));
 }
}
}
