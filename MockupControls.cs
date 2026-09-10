using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Collections.Generic;

namespace VideoShelf {
enum ShellSection { Home, Search, Collections, Downloads, Streaming, Settings }

static class UiPaint {
 public static GraphicsPath Round(Rectangle r,int radius){
  int d=Math.Max(2,radius*2);var p=new GraphicsPath();
  p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
 }
 public static void FillRound(Graphics g,Brush b,Rectangle r,int radius){using(var p=Round(r,radius))g.FillPath(b,p);}
 public static void DrawRound(Graphics g,Pen pen,Rectangle r,int radius){using(var p=Round(r,radius))g.DrawPath(pen,p);}
 public static Font IconFont(float size){try{return new Font("Segoe MDL2 Assets",size,FontStyle.Regular,GraphicsUnit.Point);}catch{return new Font("Segoe UI Symbol",size,FontStyle.Regular,GraphicsUnit.Point);}}
}

sealed class LogoMark : Control {
 public LogoMark(){Size=new Size(58,58);SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(0,0,Width-1,Height-1);
  using(var b=new SolidBrush(Color.FromArgb(12,25,38)))UiPaint.FillRound(g,b,r,5);
  using(var p=new Pen(XdolfTheme.Outline))UiPaint.DrawRound(g,p,r,5);
  using(var red=new SolidBrush(XdolfTheme.AccentRed))g.FillRectangle(red,0,0,6,Height);
  using(var blue=new SolidBrush(XdolfTheme.AccentBlue))g.FillRectangle(blue,7,0,Width-7,2);
  int[] h={13,28,20,36};for(int i=0;i<h.Length;i++){using(var b=new SolidBrush(i==3?XdolfTheme.AccentBlue:Color.FromArgb(81,118,157)))g.FillRectangle(b,14+i*8,44-h[i],6,h[i]);}
  using(var p=new Pen(Color.FromArgb(110,150,190),1))g.DrawRectangle(p,11,11,37,35);
 }
}

sealed class NavButton : Control {
 public string Glyph=""; public string Badge=""; public bool Active;
 public NavButton(string text,string glyph){Text=text;Glyph=glyph;Height=48;Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.Selectable,true);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;bool hot=ClientRectangle.Contains(PointToClient(Cursor.Position));
  if(Active||hot){using(var b=new SolidBrush(Active?Color.FromArgb(24,64,105):Color.FromArgb(17,31,44)))UiPaint.FillRound(g,b,new Rectangle(6,3,Width-12,Height-6),6);}
  if(Active)using(var b=new SolidBrush(XdolfTheme.AccentBlue))g.FillRectangle(b,6,7,4,Height-14);
  using(var f=UiPaint.IconFont(17))TextRenderer.DrawText(g,Glyph,f,new Rectangle(22,11,28,28),Active?Color.White:Color.FromArgb(190,207,224),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",10.5f,FontStyle.Regular))TextRenderer.DrawText(g,Text,f,new Rectangle(62,13,Width-112,23),Active?Color.White:XdolfTheme.Text,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  if(!string.IsNullOrEmpty(Badge)){
   var rr=new Rectangle(Width-43,11,27,26);using(var b=new SolidBrush(Color.FromArgb(24,38,53)))UiPaint.FillRound(g,b,rr,13);
   using(var p=new Pen(Color.FromArgb(42,63,82)))UiPaint.DrawRound(g,p,rr,13);
   using(var f=new Font("Segoe UI",8.5f,FontStyle.Bold))TextRenderer.DrawText(g,Badge,f,rr,Color.FromArgb(220,230,240),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
  }
  if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(12,7,Width-24,Height-14));
 }
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
 public void SetActive(bool value){if(Active==value)return;Active=value;Invalidate();}
 public void SetBadge(int value){Badge=value<=0?"0":value>99?"99+":value.ToString();Invalidate();}
}

sealed class OnlineResultCard : Control {
 public OnlineResult Result {get;private set;} public bool Selected; Image preview;
 public event EventHandler ResultSelected; public event EventHandler ResultActivated;
 public OnlineResultCard(OnlineResult result){Result=result;Height=94;MinimumSize=new Size(520,94);Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.Selectable,true);}
 public Image Preview {get{return preview;}}
 public void SetPreview(Image image){if(preview!=null)preview.Dispose();preview=image==null?null:new Bitmap(image);Invalidate();}
 protected override void Dispose(bool disposing){if(disposing&&preview!=null){preview.Dispose();preview=null;}base.Dispose(disposing);}
 protected override void OnClick(EventArgs e){base.OnClick(e);if(ResultSelected!=null)ResultSelected(this,EventArgs.Empty);Focus();}
 protected override void OnDoubleClick(EventArgs e){base.OnDoubleClick(e);if(ResultActivated!=null)ResultActivated(this,EventArgs.Empty);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter){if(ResultActivated!=null)ResultActivated(this,EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var outer=new Rectangle(0,0,Width-1,Height-1);bool hot=ClientRectangle.Contains(PointToClient(Cursor.Position));
  using(var b=new SolidBrush(Selected?Color.FromArgb(10,28,42):(hot?Color.FromArgb(13,25,35):Color.FromArgb(10,19,27))))UiPaint.FillRound(g,b,outer,7);
  using(var p=new Pen(Selected?XdolfTheme.AccentBlue:Color.FromArgb(31,48,61),Selected?2f:1f))UiPaint.DrawRound(g,p,new Rectangle(Selected?1:0,Selected?1:0,Width-(Selected?3:1),Height-(Selected?3:1)),7);
  var ir=new Rectangle(9,8,146,78);DrawPreview(g,ir);
  int left=166;int metricsWidth=245;int textRight=Math.Max(left+120,Width-metricsWidth);var tr=new Rectangle(left,10,Math.Max(50,textRight-left-8),22);
  using(var f=new Font("Segoe UI",9.8f,FontStyle.Bold))TextRenderer.DrawText(g,Result.Title,f,tr,Color.White,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  int tagX=left,tagRight=Math.Max(left,textRight-6);foreach(string tag in MetadataLabels.Tags(Result.Title,Result.Resolution)){int tagWidth=MeasureTag(tag);if(tagX+tagWidth>tagRight)break;tagX+=DrawTag(g,tag,tagX,38);}
  string secondary=(Result.Size>0?Shelf.SizeText(Result.Size):"—")+(Result.Published==DateTime.MinValue?"":"   •   "+Result.Published.ToLocalTime().ToString("yyyy-MM-dd"));
  using(var f=new Font("Segoe UI",8.4f))TextRenderer.DrawText(g,secondary,f,new Rectangle(left,66,Math.Max(40,textRight-left),18),Color.FromArgb(158,177,196),TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  int sx=Width-250,lx=Width-160,srcx=Width-78;
  using(var f=new Font("Segoe UI",9.5f,FontStyle.Bold))TextRenderer.DrawText(g,Result.Seeders.ToString("N0")+" ↑",f,new Rectangle(sx,30,78,20),Color.FromArgb(72,235,132),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",8f))TextRenderer.DrawText(g,"seeders",f,new Rectangle(sx,54,70,18),Color.FromArgb(158,177,196),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",9.5f,FontStyle.Bold))TextRenderer.DrawText(g,Result.Leechers.ToString("N0")+" ↓",f,new Rectangle(lx,30,70,20),Color.FromArgb(255,76,82),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",8f))TextRenderer.DrawText(g,"leechers",f,new Rectangle(lx,54,70,18),Color.FromArgb(158,177,196),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  using(var f=new Font("Segoe UI",8.2f))TextRenderer.DrawText(g,ShortSource(Result.Source),f,new Rectangle(srcx,37,68,20),Color.FromArgb(198,211,223),TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
 }
 void DrawPreview(Graphics g,Rectangle r){
  using(var path=UiPaint.Round(r,4)){g.SetClip(path);if(preview!=null){float scale=Math.Max((float)r.Width/preview.Width,(float)r.Height/preview.Height);float sw=r.Width/scale,sh=r.Height/scale;g.DrawImage(preview,r,new RectangleF((preview.Width-sw)/2,(preview.Height-sh)/2,sw,sh),GraphicsUnit.Pixel);}else{using(var b=new SolidBrush(Color.FromArgb(20,35,48)))g.FillRectangle(b,r);using(var p=new Pen(Color.FromArgb(49,71,91),2)){g.DrawLine(p,r.Left+14,r.Bottom-15,r.Left+54,r.Top+27);g.DrawLine(p,r.Left+54,r.Top+27,r.Left+78,r.Bottom-25);g.DrawLine(p,r.Left+78,r.Bottom-25,r.Right-15,r.Top+19);}}g.ResetClip();}
  using(var p=new Pen(Color.FromArgb(46,67,84)))UiPaint.DrawRound(g,p,r,4);
 }
 int MeasureTag(string tag){using(var f=new Font("Segoe UI",7.8f))return Math.Min(100,TextRenderer.MeasureText(tag,f).Width+14)+7;}
 int DrawTag(Graphics g,string tag,int x,int y){
  using(var f=new Font("Segoe UI",7.8f)){int w=Math.Min(100,TextRenderer.MeasureText(tag,f).Width+14);var r=new Rectangle(x,y,w,23);using(var b=new SolidBrush(Color.FromArgb(19,39,56)))UiPaint.FillRound(g,b,r,4);using(var p=new Pen(Color.FromArgb(51,84,111)))UiPaint.DrawRound(g,p,r,4);TextRenderer.DrawText(g,tag,f,r,Color.FromArgb(220,231,241),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);return w+7;}
 }
 static string ShortSource(string s){if(string.IsNullOrWhiteSpace(s))return "Source";Uri u;if(Uri.TryCreate(s,UriKind.Absolute,out u))return u.Host;return s;}
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}

static class MetadataLabels {
 public static string[] Tags(string title,string resolution){
  var tags=new List<string>();string t=title??"";
  if(Regex.IsMatch(t,@"\b(OVA|OAV)\b",RegexOptions.IgnoreCase))tags.Add("OVA");
  else if(Regex.IsMatch(t,@"\b(S\d{1,2}|season|episode|E\d{1,3})\b",RegexOptions.IgnoreCase))tags.Add("TV Series");
  else if(Regex.IsMatch(t,@"\b(movie|film)\b",RegexOptions.IgnoreCase))tags.Add("Movie");
  else tags.Add("Video");
  if(!string.IsNullOrWhiteSpace(resolution)&&!resolution.Equals("Other",StringComparison.OrdinalIgnoreCase))tags.Add(resolution);
  if(Regex.IsMatch(t,@"\b(x265|h\.?265|hevc)\b",RegexOptions.IgnoreCase))tags.Add("x265");else if(Regex.IsMatch(t,@"\b(x264|h\.?264|avc)\b",RegexOptions.IgnoreCase))tags.Add("x264");
  if(Regex.IsMatch(t,@"\bdual[ ._-]?audio\b",RegexOptions.IgnoreCase))tags.Add("Dual Audio");
  if(Regex.IsMatch(t,@"\b(subbed|subs?|multi[ ._-]?sub)\b",RegexOptions.IgnoreCase))tags.Add("Subbed");
  if(Regex.IsMatch(t,@"\bdubbed\b",RegexOptions.IgnoreCase))tags.Add("Dubbed");
  while(tags.Count>5)tags.RemoveAt(tags.Count-1);return tags.ToArray();
 }
 public static string Category(OnlineResult r){string s=(r.Source??"")+" "+(r.Title??"");if(s.IndexOf("nyaa",StringComparison.OrdinalIgnoreCase)>=0)return "Anime";if(Regex.IsMatch(s,@"\b(anime|OVA|OAV)\b",RegexOptions.IgnoreCase))return "Anime";if(Regex.IsMatch(s,@"\b(movie|film)\b",RegexOptions.IgnoreCase))return "Movies";if(Regex.IsMatch(s,@"\b(S\d{1,2}|season|episode|E\d{1,3})\b",RegexOptions.IgnoreCase))return "TV";return "Other";}
}
}
