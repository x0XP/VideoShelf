using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VideoShelf {
static class Program {
 [STAThread] static void Main(string[] args) {
  if(args!=null&&args.Any(a=>a.Equals("--self-test",StringComparison.OrdinalIgnoreCase))){
   try{FolderNaming.SelfTest();Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;
  }
  int libraryVideosArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-library-videos",StringComparison.OrdinalIgnoreCase));
  if(libraryVideosArg>=0){
   try{
    if(libraryVideosArg+4>=args.Length)throw new ArgumentException("Usage: --screenshot-library-videos <root> <collection> <output.png> <expected-count>");
    int expected;if(!int.TryParse(args[libraryVideosArg+4],out expected)||expected<1)throw new ArgumentException("Expected video count must be a positive integer.");
    ScreenshotHarness.CaptureLibraryVideos(args[libraryVideosArg+1],args[libraryVideosArg+2],args[libraryVideosArg+3],expected);
    Environment.Exit(0);
   }catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;
  }
  int screenshotArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-rezero",StringComparison.OrdinalIgnoreCase));
  if(screenshotArg>=0){
   try{
    string output=(screenshotArg+1<args.Length&&args[screenshotArg+1].Length>0)?args[screenshotArg+1]:Path.Combine(Environment.CurrentDirectory,"VideoShelf-rezero.png");
    ScreenshotHarness.CaptureReZero(output);
    Environment.Exit(0);
   }catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;
  }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Shelf());
 }
}
sealed class Person { public string Path, Name; public Image Photo; public string PhotoSource="", PhotoError=""; public int PhotoVersion; }
sealed class Video { public string Path, Name, Relative; public long Size; public DateTime Modified; }
sealed class Portrait : Button {
 public Person Person;
 public Portrait(Person person) { Person=person; Size=new Size(204,264); Margin=new Padding(0,0,20,20); FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; Text=person.Name; AccessibleName=person.Name; }
 protected override void OnPaint(PaintEventArgs e) {
  Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.None;
  bool hot=Focused || ClientRectangle.Contains(PointToClient(Cursor.Position));
  g.Clear(hot ? XdolfTheme.Hover : XdolfTheme.Panel);
  using(var border=new Pen(XdolfTheme.Outline))g.DrawRectangle(border,0,0,Width-1,Height-1);
  using(var red=new SolidBrush(XdolfTheme.AccentRed))g.FillRectangle(red,0,0,3,Height);
  using(var blue=new SolidBrush(XdolfTheme.AccentBlue))g.FillRectangle(blue,3,0,Width-3,2);
  Rectangle r=new Rectangle(10,10,184,190);
  if(Person.Photo!=null) { Image im=Person.Photo; float scale=Math.Max((float)r.Width/im.Width,(float)r.Height/im.Height); float w=r.Width/scale,h=r.Height/scale; g.DrawImage(im,r,new RectangleF((im.Width-w)/2,(im.Height-h)/2,w,h),GraphicsUnit.Pixel); }
  else { using(Brush b=new SolidBrush(XdolfTheme.PanelRaised)) g.FillRectangle(b,r); using(Font f=new Font("Segoe UI",44,FontStyle.Bold)) TextRenderer.DrawText(g,Person.Name.Length==0?"?":Person.Name.Substring(0,1).ToUpper(),f,r,XdolfTheme.AccentBlue,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); }
  using(var divider=new Pen(Color.FromArgb(85,XdolfTheme.Outline)))g.DrawLine(divider,10,205,194,205);
  TextRenderer.DrawText(g,Person.Name,Font,new Rectangle(12,212,180,24),XdolfTheme.TextStrong,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  using(Font f=new Font("Segoe UI",9)) TextRenderer.DrawText(g,"View videos  →",f,new Rectangle(12,239,180,18),XdolfTheme.Muted,TextFormatFlags.SingleLine);
  if(Focused) ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
 }
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}
}
