using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
static class XdolfTheme {
 public static readonly Color Background=Color.FromArgb(6,13,20);
 public static readonly Color Sidebar=Color.FromArgb(7,15,22);
 public static readonly Color Panel=Color.FromArgb(9,18,26);
 public static readonly Color PanelRaised=Color.FromArgb(13,25,36);
 public static readonly Color Hover=Color.FromArgb(18,34,48);
 public static readonly Color Outline=Color.FromArgb(35,54,70);
 public static readonly Color AccentBlue=Color.FromArgb(31,148,255);
 public static readonly Color AccentRed=Color.FromArgb(255,58,66);
 public static readonly Color Success=Color.FromArgb(72,235,132);
 public static readonly Color Text=Color.FromArgb(215,226,236);
 public static readonly Color TextStrong=Color.FromArgb(246,249,252);
 public static readonly Color Muted=Color.FromArgb(153,171,190);
 public static readonly Color Warning=Color.FromArgb(255,190,83);
 public static readonly Color Error=Color.FromArgb(255,93,108);

 public static void ConfigureToolTip(ToolTip tip){
  tip.OwnerDraw=true;tip.ShowAlways=true;tip.InitialDelay=280;tip.ReshowDelay=90;tip.AutoPopDelay=7000;tip.UseAnimation=false;tip.UseFading=false;tip.Popup+=OnPopup;tip.Draw+=OnDraw;
 }
 static string TitleFor(Control control){
  var portrait=control as Portrait;if(portrait!=null&&portrait.Person!=null)return portrait.Person.Name;
  var button=control as Button;if(button!=null&&!string.IsNullOrWhiteSpace(button.Text))return button.Text.Trim().TrimStart('←','▶','+','↓',' ');
  if(!string.IsNullOrWhiteSpace(control.AccessibleName))return control.AccessibleName;if(control is ComboBox)return "Options";if(control is ListView)return "Result details";return "VideoShelf";
 }
 static string BodyFor(Control control,string text){string title=TitleFor(control);string value=(text??"").Trim();if(value.StartsWith(title+"\n",StringComparison.OrdinalIgnoreCase))value=value.Substring(title.Length+1).Trim();return value;}
 static void OnPopup(object sender,PopupEventArgs e){
  var tip=sender as ToolTip;if(tip==null||e.AssociatedControl==null)return;string body=BodyFor(e.AssociatedControl,tip.GetToolTip(e.AssociatedControl));const int maxWidth=340,minWidth=190;
  var bodySize=TextRenderer.MeasureText(body.Length==0?" ":body,SystemFonts.MessageBoxFont,new Size(maxWidth-24,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);string title=TitleFor(e.AssociatedControl);var titleSize=TextRenderer.MeasureText(title,SystemFonts.MessageBoxFont,new Size(maxWidth-86,40),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);e.ToolTipSize=new Size(Math.Max(minWidth,Math.Min(maxWidth,Math.Max(bodySize.Width+24,titleSize.Width+82))),bodySize.Height+39);
 }
 static void OnDraw(object sender,DrawToolTipEventArgs e){
  string title=TitleFor(e.AssociatedControl),body=BodyFor(e.AssociatedControl,e.ToolTipText);Rectangle bounds=e.Bounds;
  using(var shadow=new SolidBrush(Color.FromArgb(150,0,0,0)))e.Graphics.FillRectangle(shadow,new Rectangle(bounds.X+2,bounds.Y+2,bounds.Width-2,bounds.Height-2));
  using(var fill=new SolidBrush(Background))e.Graphics.FillRectangle(fill,new Rectangle(bounds.X,bounds.Y,bounds.Width-2,bounds.Height-2));using(var border=new Pen(Outline))e.Graphics.DrawRectangle(border,bounds.X,bounds.Y,bounds.Width-3,bounds.Height-3);
  using(var red=new SolidBrush(AccentRed))e.Graphics.FillRectangle(red,bounds.X,bounds.Y,3,bounds.Height-2);using(var blue=new SolidBrush(AccentBlue))e.Graphics.FillRectangle(blue,bounds.X+3,bounds.Y,bounds.Width-5,2);
  TextRenderer.DrawText(e.Graphics,title,SystemFonts.MessageBoxFont,new Rectangle(bounds.X+9,bounds.Y+7,bounds.Width-78,18),TextStrong,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);TextRenderer.DrawText(e.Graphics,"INFO",SystemFonts.MessageBoxFont,new Rectangle(bounds.Right-60,bounds.Y+7,48,18),AccentBlue,TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
  using(var divider=new Pen(Color.FromArgb(85,Outline)))e.Graphics.DrawLine(divider,bounds.X+9,bounds.Y+27,bounds.Right-11,bounds.Y+27);TextRenderer.DrawText(e.Graphics,body,SystemFonts.MessageBoxFont,new Rectangle(bounds.X+9,bounds.Y+32,bounds.Width-20,bounds.Height-37),Text,TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);
 }
 public static void StyleButton(Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=1;b.FlatAppearance.BorderColor=Outline;b.FlatAppearance.MouseOverBackColor=Hover;b.FlatAppearance.MouseDownBackColor=Color.FromArgb(23,43,59);b.BackColor=PanelRaised;b.ForeColor=TextStrong;b.Cursor=Cursors.Hand;}
 public static void StylePrimary(Button b){StyleButton(b);b.FlatAppearance.BorderColor=AccentBlue;b.BackColor=Color.FromArgb(12,30,45);}
 public static void StyleDanger(Button b){StyleButton(b);b.FlatAppearance.BorderColor=AccentRed;b.BackColor=Color.FromArgb(29,18,24);}
 public static void StyleInput(Control c){c.BackColor=Color.FromArgb(10,20,29);c.ForeColor=Text;}
}
}
