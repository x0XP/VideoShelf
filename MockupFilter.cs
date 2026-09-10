using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

sealed class DarkFlowLayoutPanel : FlowLayoutPanel {
 const int SB_VERT=1;const int WM_NCPAINT=0x85,WM_SIZE=0x5,WM_VSCROLL=0x115;
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowScrollBar(IntPtr hWnd,int wBar,bool bShow);
 public DarkFlowLayoutPanel(){DoubleBuffered=true;}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(IsHandleCreated&&(m.Msg==WM_NCPAINT||m.Msg==WM_SIZE||m.Msg==WM_VSCROLL))ShowScrollBar(Handle,SB_VERT,false);}
 protected override void OnMouseWheel(MouseEventArgs e){int current=-AutoScrollPosition.Y;int next=Math.Max(0,current-(e.Delta/120)*72);AutoScrollPosition=new Point(0,next);if(IsHandleCreated)ShowScrollBar(Handle,SB_VERT,false);}
 protected override void OnLayout(LayoutEventArgs levent){base.OnLayout(levent);if(IsHandleCreated)ShowScrollBar(Handle,SB_VERT,false);}
}
}
