using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace VideoShelf {
static class AppBrand {
 static Icon cached;
 static readonly int[] NativeIconSizes=new[]{16,24,32,48,64,96,128,256};
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

 static int NearestNativeSize(int requested){
  int best=NativeIconSizes[0],distance=Math.Abs(best-requested);
  for(int i=1;i<NativeIconSizes.Length;i++){
   int d=Math.Abs(NativeIconSizes[i]-requested);
   if(d<distance){best=NativeIconSizes[i];distance=d;}
  }
  return best;
 }

 public static Bitmap Bitmap(int width,int height){
  int w=Math.Max(1,width),h=Math.Max(1,height);
  int requested=Math.Max(w,h);
  var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  Image source=null;
  try{
   // For small UI uses, prefer the native multi-size ICO frames instead of
   // shrinking the 512px master. Each ICO frame is rendered independently at
   // its target resolution, so title-bar/sidebar icons stay sharper.
   if(requested<=64&&File.Exists(IconPath)){
    try{
     int native=NearestNativeSize(requested);
     using(var frame=new Icon(IconPath,new Size(native,native)))source=frame.ToBitmap();
    }catch{}
   }
   // Larger branding uses the transparent 512px master to avoid upscaling a
   // small Windows icon frame.
   if(source==null){try{if(File.Exists(PngPath))source=Image.FromFile(PngPath);}catch{}}
   if(source==null&&File.Exists(IconPath)){
    try{
     int native=NearestNativeSize(Math.Min(256,requested));
     using(var frame=new Icon(IconPath,new Size(native,native)))source=frame.ToBitmap();
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