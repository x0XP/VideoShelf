using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VideoShelf {
sealed class MockupFilter : Control {
 readonly ComboBox source;
 readonly ContextMenuStrip menu=new ContextMenuStrip();
 bool hot;
 public MockupFilter(ComboBox source){
  this.source=source;Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.Selectable,true);
  menu.BackColor=Color.FromArgb(8,17,25);menu.ForeColor=XdolfTheme.Text;menu.ShowImageMargin=false;menu.ShowCheckMargin=false;menu.Padding=new Padding(1);menu.Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors());
  source.SelectedIndexChanged+=delegate{Invalidate();};source.TextChanged+=delegate{Invalidate();};
 }
 public string DisplayText {get{return source.SelectedItem==null?(source.Text??""):Convert.ToString(source.SelectedItem);}}
 protected override void OnPaint(PaintEventArgs e){
  Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Rectangle r=new Rectangle(0,0,Width-1,Height-1);
  using(var b=new SolidBrush(hot?Color.FromArgb(14,27,38):Color.FromArgb(10,20,29)))UiPaint.FillRound(g,b,r,5);
  using(var p=new Pen(hot?Color.FromArgb(68,97,121):Color.FromArgb(48,69,86)))UiPaint.DrawRound(g,p,r,5);
  using(var f=new Font("Segoe UI",9.6f))TextRenderer.DrawText(g,DisplayText,f,new Rectangle(12,0,Width-42,Height),Color.FromArgb(229,236,243),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  int cx=Width-20,cy=Height/2;using(var p=new Pen(Color.FromArgb(177,198,217),1.3f)){g.DrawLine(p,cx-4,cy-2,cx,cy+2);g.DrawLine(p,cx,cy+2,cx+4,cy-2);}
  if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
 }
 void OpenMenu(){
  menu.Items.Clear();menu.MinimumSize=new Size(Width,0);
  for(int i=0;i<source.Items.Count;i++){
   string text=Convert.ToString(source.Items[i]);
   var item=new ToolStripMenuItem(text){Tag=i,Checked=i==source.SelectedIndex,CheckOnClick=false,AutoSize=false,Width=Math.Max(120,Width-4),Height=30,BackColor=Color.FromArgb(8,17,25),ForeColor=XdolfTheme.Text,Padding=new Padding(10,0,8,0)};
   item.Click+=delegate(object sender,EventArgs e){var clicked=sender as ToolStripMenuItem;if(clicked!=null)source.SelectedIndex=(int)clicked.Tag;};menu.Items.Add(item);
  }
  if(menu.Items.Count>0)menu.Show(this,new Point(0,Height));
 }
 protected override void OnClick(EventArgs e){base.OnClick(e);if(Enabled)OpenMenu();}
 protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hot=false;Invalidate();base.OnMouseLeave(e);}protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space||e.KeyCode==Keys.Down){OpenMenu();e.Handled=true;}base.OnKeyDown(e);}
 protected override void Dispose(bool disposing){if(disposing)menu.Dispose();base.Dispose(disposing);}
}

sealed class DarkMenuColors : ProfessionalColorTable {
 public DarkMenuColors(){UseSystemColors=false;}
 public override Color ToolStripDropDownBackground { get { return Color.FromArgb(8,17,25); } }
 public override Color MenuBorder { get { return XdolfTheme.Outline; } }
 public override Color MenuItemBorder { get { return Color.FromArgb(42,78,106); } }
 public override Color MenuItemSelected { get { return Color.FromArgb(20,48,72); } }
 public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(20,48,72); } }
 public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(20,48,72); } }
 public override Color ImageMarginGradientBegin { get { return Color.FromArgb(8,17,25); } }
 public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(8,17,25); } }
 public override Color ImageMarginGradientEnd { get { return Color.FromArgb(8,17,25); } }
 public override Color SeparatorDark { get { return XdolfTheme.Outline; } }
 public override Color SeparatorLight { get { return XdolfTheme.Outline; } }
}

sealed class InspectorMetadataView : Control {
 string raw="";
 public string RawText {get{return raw;}set{raw=value??"";Invalidate();}}
 public InspectorMetadataView(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true);BackColor=Color.FromArgb(7,16,23);}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);Graphics g=e.Graphics;string[] lines=(raw??"").Replace("\r","").Split('\n');int y=2,rows=0;
  foreach(string input in lines){string line=input.Trim();if(line.Length==0)continue;if(rows<5&&line.IndexOf('\t')>=0){string[] pieces=line.Split(new[]{'\t'},StringSplitOptions.RemoveEmptyEntries);if(pieces.Length>=2){string label=pieces[0].Trim(),value=pieces[pieces.Length-1].Trim();using(var f=new Font("Segoe UI",9.2f))TextRenderer.DrawText(g,label,f,new Rectangle(0,y,118,20),Color.FromArgb(173,194,213),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);Color valueColor=label.StartsWith("Seeder",StringComparison.OrdinalIgnoreCase)?XdolfTheme.Success:label.StartsWith("Leecher",StringComparison.OrdinalIgnoreCase)?XdolfTheme.AccentRed:Color.White;using(var f=new Font("Segoe UI",9.2f,label.StartsWith("Seeder",StringComparison.OrdinalIgnoreCase)||label.StartsWith("Leecher",StringComparison.OrdinalIgnoreCase)?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(g,value,f,new Rectangle(150,y,Math.Max(0,Width-150),20),valueColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);y+=31;rows++;continue;}}
   if(rows>=5||line.IndexOf('\t')<0){y+=7;using(var f=new Font("Segoe UI",9.1f))TextRenderer.DrawText(g,line,f,new Rectangle(0,y,Width,Math.Max(0,Height-y)),Color.FromArgb(192,207,221),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);break;}
  }
 }
}

static class NativeDarkScroll {
 [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd,string pszSubAppName,string pszSubIdList);
 public static void Apply(Control control){
  if(control==null||control.IsDisposed)return;
  try{if(!control.IsHandleCreated)control.CreateControl();SetWindowTheme(control.Handle,"DarkMode_Explorer",null);control.Invalidate(true);}catch{}
 }
}

sealed class SearchScrollRail : Control {
 readonly ScrollableControl target;
 bool dragging,hot;
 int dragOffset;
 public SearchScrollRail(ScrollableControl target){
  this.target=target;Cursor=Cursors.Hand;TabStop=false;BackColor=XdolfTheme.Background;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true);
  target.Scroll+=delegate{Invalidate();};target.MouseWheel+=delegate{Invalidate();};target.ControlAdded+=delegate{Invalidate();};target.ControlRemoved+=delegate{Invalidate();};target.Resize+=delegate{Invalidate();};
 }
 int ContentHeight(){int total=target.Padding.Vertical;foreach(Control c in target.Controls)total+=c.Height+c.Margin.Vertical;return Math.Max(target.ClientSize.Height,total);}
 int MaxScroll(){return Math.Max(0,ContentHeight()-target.ClientSize.Height);}
 int CurrentScroll(){int max=MaxScroll();return Math.Max(0,Math.Min(max,-target.AutoScrollPosition.Y));}
 Rectangle Thumb(){int max=MaxScroll();if(max<=0||Height<=12)return Rectangle.Empty;int track=Math.Max(1,Height-8),content=ContentHeight();int thumb=Math.Max(38,Math.Min(track,(int)Math.Round(track*(target.ClientSize.Height/(double)Math.Max(1,content)))));int travel=Math.Max(0,track-thumb);int y=4+(max==0?0:(int)Math.Round(travel*(CurrentScroll()/(double)max)));return new Rectangle(4,y,Math.Max(4,Width-8),thumb);}
 void SetScroll(int value){int max=MaxScroll();value=Math.Max(0,Math.Min(max,value));target.AutoScrollPosition=new Point(0,value);Invalidate();target.Invalidate();}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);using(var b=new SolidBrush(Color.FromArgb(6,13,20)))e.Graphics.FillRectangle(b,ClientRectangle);var thumb=Thumb();if(thumb.IsEmpty)return;using(var track=new SolidBrush(Color.FromArgb(11,23,32)))UiPaint.FillRound(e.Graphics,track,new Rectangle(5,3,Math.Max(3,Width-10),Math.Max(4,Height-6)),4);using(var b=new SolidBrush(hot||dragging?Color.FromArgb(83,116,143):Color.FromArgb(55,80,101)))UiPaint.FillRound(e.Graphics,b,thumb,4);
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;var thumb=Thumb();if(thumb.IsEmpty)return;if(thumb.Contains(e.Location)){dragging=true;dragOffset=e.Y-thumb.Y;Capture=true;}else{int max=MaxScroll();int targetY=e.Y-thumb.Height/2;int travel=Math.Max(1,Height-8-thumb.Height);SetScroll((int)Math.Round(max*(Math.Max(0,Math.Min(travel,targetY-4))/(double)travel)));}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);hot=true;if(!dragging)return;var thumb=Thumb();int max=MaxScroll();int travel=Math.Max(1,Height-8-thumb.Height);int y=Math.Max(0,Math.Min(travel,e.Y-dragOffset-4));SetScroll((int)Math.Round(max*(y/(double)travel)));}
 protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){if(!dragging)hot=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseWheel(MouseEventArgs e){SetScroll(CurrentScroll()-(e.Delta/120)*94);base.OnMouseWheel(e);}
}
}
