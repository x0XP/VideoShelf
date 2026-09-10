using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace VideoShelf {
static class Program {
 [STAThread] static void Main(string[] args) {
  if(args!=null&&args.Any(a=>a.Equals("--self-test",StringComparison.OrdinalIgnoreCase))){try{FolderNaming.SelfTest();BuiltInOnlineSearch.SelfTest();Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int builtInArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--test-built-in-online",StringComparison.OrdinalIgnoreCase));
  if(builtInArg>=0){try{string query=(builtInArg+1<args.Length&&args[builtInArg+1].Length>0)?args[builtInArg+1]:"re zero";using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(18))){var found=BuiltInOnlineSearch.Search(query,timeout.Token).GetAwaiter().GetResult();if(found.Count==0)throw new InvalidOperationException("Built-in online search returned no seeded results for "+query+".");if(found.Any(r=>r.Seeders<=0||string.IsNullOrWhiteSpace(r.Link)))throw new InvalidOperationException("Built-in online search returned an invalid or zero-seeder result.");Console.WriteLine("Built-in online search returned "+found.Count+" seeded result(s). Sources: "+string.Join(", ",found.Select(r=>r.Source).Distinct(StringComparer.OrdinalIgnoreCase).Take(8)));}Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int builtInShotArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-built-in-online",StringComparison.OrdinalIgnoreCase));
  if(builtInShotArg>=0){try{if(builtInShotArg+4>=args.Length)throw new ArgumentException("Usage: --screenshot-built-in-online <display-name> <query> <output.png> <expected-count>");int expected;if(!int.TryParse(args[builtInShotArg+4],out expected)||expected<1)throw new ArgumentException("Expected result count must be a positive integer.");ScreenshotHarness.CaptureBuiltInOnlineResults(args[builtInShotArg+1],args[builtInShotArg+2],args[builtInShotArg+3],expected);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int onlineArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-online",StringComparison.OrdinalIgnoreCase));
  if(onlineArg>=0){try{if(onlineArg+5>=args.Length)throw new ArgumentException("Usage: --screenshot-online <display-name> <query> <source-url> <output.png> <expected-count>");int expected;if(!int.TryParse(args[onlineArg+5],out expected)||expected<1)throw new ArgumentException("Expected result count must be a positive integer.");ScreenshotHarness.CaptureOnlineResults(args[onlineArg+1],args[onlineArg+2],args[onlineArg+3],args[onlineArg+4],expected);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int mockArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-mockup-search",StringComparison.OrdinalIgnoreCase));
  if(mockArg>=0){try{string output=(mockArg+1<args.Length&&args[mockArg+1].Length>0)?args[mockArg+1]:Path.Combine(Environment.CurrentDirectory,"VideoShelf-mockup-search.png");ScreenshotHarness.CaptureMockupSearch(output);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int homeLayoutArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-home-layout",StringComparison.OrdinalIgnoreCase));
  if(homeLayoutArg>=0){try{string output=(homeLayoutArg+1<args.Length&&args[homeLayoutArg+1].Length>0)?args[homeLayoutArg+1]:Path.Combine(Environment.CurrentDirectory,"VideoShelf-home-layout.png");ScreenshotHarness.CaptureHomeLayout(output);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int detailArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-rezero-detail",StringComparison.OrdinalIgnoreCase));
  if(detailArg>=0){try{string output=(detailArg+1<args.Length&&args[detailArg+1].Length>0)?args[detailArg+1]:Path.Combine(Environment.CurrentDirectory,"VideoShelf-rezero-detail.png");ScreenshotHarness.CaptureReZeroDetail(output);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  int screenshotArg=args==null?-1:Array.FindIndex(args,a=>a.Equals("--screenshot-rezero",StringComparison.OrdinalIgnoreCase));
  if(screenshotArg>=0){try{string output=(screenshotArg+1<args.Length&&args[screenshotArg+1].Length>0)?args[screenshotArg+1]:Path.Combine(Environment.CurrentDirectory,"VideoShelf-rezero.png");ScreenshotHarness.CaptureReZero(output);Environment.Exit(0);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.Exit(1);}return;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Shelf());
 }
}
sealed class Person { public string Path,Name; public Image Photo; public string PhotoSource="",PhotoError=""; public int PhotoVersion; }
sealed class Video { public string Path,Name,Relative; public long Size; public DateTime Modified; }
sealed class Portrait : Button {
 public Person Person;
 public Portrait(Person person){Person=person;Size=new Size(222,292);Margin=new Padding(0,0,18,18);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Text=person.Name;AccessibleName=person.Name;TabStop=true;}
 protected override void OnPaint(PaintEventArgs e){
  Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;bool hot=Focused||ClientRectangle.Contains(PointToClient(Cursor.Position));var outer=new Rectangle(0,0,Width-1,Height-1);
  using(var b=new SolidBrush(hot?Color.FromArgb(15,30,42):Color.FromArgb(9,19,27)))UiPaint.FillRound(g,b,outer,7);using(var p=new Pen(hot?Color.FromArgb(48,86,116):XdolfTheme.Outline))UiPaint.DrawRound(g,p,outer,7);
  Rectangle imageRect=new Rectangle(10,10,202,215);using(var path=UiPaint.Round(imageRect,5)){g.SetClip(path);if(Person.Photo!=null){Image im=Person.Photo;float scale=Math.Max((float)imageRect.Width/im.Width,(float)imageRect.Height/im.Height);float w=imageRect.Width/scale,h=imageRect.Height/scale;g.DrawImage(im,imageRect,new RectangleF((im.Width-w)/2,(im.Height-h)/2,w,h),GraphicsUnit.Pixel);}else{using(var b=new SolidBrush(Color.FromArgb(18,33,45)))g.FillRectangle(b,imageRect);using(var f=new Font("Segoe UI",44,FontStyle.Bold))TextRenderer.DrawText(g,Person.Name.Length==0?"?":Person.Name.Substring(0,1).ToUpperInvariant(),f,imageRect,XdolfTheme.AccentBlue,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);}g.ResetClip();}
  using(var p=new Pen(Color.FromArgb(43,65,82)))UiPaint.DrawRound(g,p,imageRect,5);using(var blue=new SolidBrush(XdolfTheme.AccentBlue))g.FillRectangle(blue,10,10,202,2);using(var red=new SolidBrush(XdolfTheme.AccentRed))g.FillRectangle(red,10,12,3,213);
  using(var f=new Font("Segoe UI",10.5f,FontStyle.Bold))TextRenderer.DrawText(g,Person.Name,f,new Rectangle(13,239,196,24),Color.White,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",8.7f))TextRenderer.DrawText(g,"View videos   →",f,new Rectangle(13,267,196,18),XdolfTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10));
 }
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}
}
