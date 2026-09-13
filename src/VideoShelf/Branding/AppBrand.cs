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

 public static Icon IconForDpi(int dpi){
  int safeDpi=Math.Max(96,dpi);
  int requested=(int)Math.Round(32d*safeDpi/96d);
  int native=NearestNativeSize(Math.Max(32,Math.Min(256,requested)));
  try{if(File.Exists(IconPath))return new Icon(IconPath,new Size(native,native));}catch{}
  try{return (Icon)Icon.Clone();}catch{return (Icon)SystemIcons.Application.Clone();}
 }

 // The full VideoShelf artwork contains several fine details which are useful at
 // normal branding sizes but collapse into coloured noise in the 23px custom
 // title bar. Keep a deliberately reduced mark for that one tiny UI surface:
 // the dark tile, blue/red edge accents and the white play glyph.
 public static Bitmap TitleBitmap(int width,int height){
  int w=Math.Max(1,width),h=Math.Max(1,height);
  var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(output)){
   g.Clear(Color.Transparent);
   g.CompositingMode=CompositingMode.SourceOver;
   g.CompositingQuality=CompositingQuality.HighQuality;
   g.SmoothingMode=SmoothingMode.AntiAlias;
   g.PixelOffsetMode=PixelOffsetMode.HighQuality;

   int side=Math.Max(12,Math.Min(w,h)-4);
   float x=(w-side)/2f,y=(h-side)/2f;
   var rect=new RectangleF(x+.5f,y+.5f,side-1f,side-1f);
   float radius=Math.Max(2f,side*.18f),d=radius*2f;
   using(var path=new GraphicsPath()){
    path.AddArc(rect.Left,rect.Top,d,d,180,90);
    path.AddArc(rect.Right-d,rect.Top,d,d,270,90);
    path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);
    path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);
    path.CloseFigure();
    using(var body=new SolidBrush(Color.FromArgb(255,7,18,27)))g.FillPath(body,path);
    using(var border=new Pen(Color.FromArgb(255,53,82,101),1f))g.DrawPath(border,path);
   }

   float accentWidth=Math.Max(1.5f,side*.095f);
   using(var blue=new Pen(Color.FromArgb(255,0,157,235),accentWidth))
   using(var red=new Pen(Color.FromArgb(255,255,58,66),accentWidth)){
    blue.StartCap=blue.EndCap=LineCap.Round;
    red.StartCap=red.EndCap=LineCap.Round;
    g.DrawLine(blue,x+side*.22f,y+side*.17f,x+side*.76f,y+side*.17f);
    g.DrawLine(red,x+side*.17f,y+side*.23f,x+side*.17f,y+side*.75f);
   }

   PointF[] play={
    new PointF(x+side*.45f,y+side*.31f),
    new PointF(x+side*.45f,y+side*.69f),
    new PointF(x+side*.76f,y+side*.50f)
   };
   using(var white=new SolidBrush(Color.FromArgb(250,244,248,250)))g.FillPolygon(white,play);
  }
  return output;
 }

 public static Bitmap Bitmap(int width,int height){
  int w=Math.Max(1,width),h=Math.Max(1,height);
  int requested=Math.Max(w,h);
  var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  Image source=null;
  try{
   // For small UI uses, prefer the native multi-size ICO frames instead of
   // shrinking the 512px master. Each ICO frame is rendered independently at
   // its target resolution, so sidebar and compact branding stays sharper.
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