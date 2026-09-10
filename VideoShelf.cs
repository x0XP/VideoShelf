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
 [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Shelf()); }
}
sealed class Person { public string Path, Name; public Image Photo; public string PhotoSource="", PhotoError=""; public int PhotoVersion; }
sealed class Video { public string Path, Name, Relative; public long Size; public DateTime Modified; }
sealed class Portrait : Button {
 public Person Person;
 public Portrait(Person person) { Person=person; Size=new Size(204,264); Margin=new Padding(0,0,20,20); FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; Text=person.Name; AccessibleName=person.Name; }
 protected override void OnPaint(PaintEventArgs e) {
  Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Focused || ClientRectangle.Contains(PointToClient(Cursor.Position)) ? Color.FromArgb(43,51,69) : Color.FromArgb(28,34,47));
  Rectangle r=new Rectangle(10,10,184,190);
  if(Person.Photo!=null) { Image im=Person.Photo; float scale=Math.Max((float)r.Width/im.Width,(float)r.Height/im.Height); float w=r.Width/scale,h=r.Height/scale; g.DrawImage(im,r,new RectangleF((im.Width-w)/2,(im.Height-h)/2,w,h),GraphicsUnit.Pixel); }
  else { using(Brush b=new SolidBrush(Color.FromArgb(46,57,79))) g.FillRectangle(b,r); using(Font f=new Font("Segoe UI",44,FontStyle.Bold)) TextRenderer.DrawText(g,Person.Name.Length==0?"?":Person.Name.Substring(0,1).ToUpper(),f,r,Color.FromArgb(151,180,236),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); }
  TextRenderer.DrawText(g,Person.Name,Font,new Rectangle(12,211,180,24),Color.White,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  using(Font f=new Font("Segoe UI",9)) TextRenderer.DrawText(g,"View videos  →",f,new Rectangle(12,238,180,18),Color.FromArgb(148,167,200));
  if(Focused) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
 }
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
}
}
