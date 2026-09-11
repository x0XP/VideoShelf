using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace VideoShelf {
static class AppBrand {
 static Icon cached;
 static string IconPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"VideoShelf.ico"); } }
 static string PngPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"VideoShelf.png"); } }

 public static Icon Icon {
  get {
   if(cached!=null)return cached;
   try{if(File.Exists(IconPath))cached=new Icon(IconPath,new Size(32,32));}catch{}
   if(cached==null){try{cached=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}}
   if(cached==null)cached=SystemIcons.Application;
   return cached;
  }
 }

 public static Bitmap Bitmap(int width,int height){
  int w=Math.Max(1,width),h=Math.Max(1,height);
  var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  Image source=null;
  try{
   try{if(File.Exists(PngPath))source=Image.FromFile(PngPath);}catch{}
   if(source==null&&File.Exists(IconPath)){
    try{
     int requested=Math.Max(16,Math.Min(64,Math.Max(w,h)));
     using(var frame=new Icon(IconPath,new Size(requested,requested)))source=frame.ToBitmap();
    }catch{}
   }
   if(source==null){try{source=Icon.ToBitmap();}catch{}}
   using(var g=Graphics.FromImage(output)){
    g.Clear(Color.Transparent);
    g.CompositingMode=CompositingMode.SourceOver;
    g.CompositingQuality=CompositingQuality.HighQuality;
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode=PixelOffsetMode.HighQuality;
    g.SmoothingMode=SmoothingMode.HighQuality;
    if(source!=null)g.DrawImage(source,new Rectangle(0,0,w,h));
   }
  } finally {if(source!=null)source.Dispose();}
  return output;
 }
}
}
