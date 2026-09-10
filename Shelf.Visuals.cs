using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool extendedVisualsApplied,fittingFileColumns;
 Panel queryFrame;
 Label queryGlyph;

 void ApplyExtendedVisuals(){
  if(extendedVisualsApplied)return;
  extendedVisualsApplied=true;
  ConfigureCombo(sourceFilter);ConfigureCombo(categoryFilter);ConfigureCombo(resolution);ConfigureCombo(homeSort);
  BuildSearchFrame();
  ConfigureFileList();
 }
 void ConfigureCombo(ComboBox combo){
  combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=30;combo.DropDownHeight=240;combo.FlatStyle=FlatStyle.Flat;combo.BackColor=Color.FromArgb(10,20,29);combo.ForeColor=XdolfTheme.Text;
  combo.DrawItem+=DrawComboItem;
 }
 void DrawComboItem(object sender,DrawItemEventArgs e){
  ComboBox combo=sender as ComboBox;if(combo==null)return;bool selected=(e.State&DrawItemState.Selected)!=0;Color bg=selected?Color.FromArgb(24,64,105):Color.FromArgb(10,20,29);using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);
  string text=e.Index>=0&&e.Index<combo.Items.Count?Convert.ToString(combo.Items[e.Index]):combo.Text;TextRenderer.DrawText(e.Graphics,text??"",combo.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height),selected?Color.White:XdolfTheme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);e.DrawFocusRectangle();
 }
 void BuildSearchFrame(){
  Control parent=onlineQuery.Parent;if(parent==null)return;
  queryFrame=new Panel{BackColor=Color.FromArgb(10,20,29),TabStop=false};queryFrame.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Color.FromArgb(53,74,91)))e.Graphics.DrawRectangle(p,0,0,queryFrame.Width-1,queryFrame.Height-1);};queryFrame.Click+=delegate{onlineQuery.Focus();};
  parent.Controls.Add(queryFrame);onlineQuery.Parent=queryFrame;onlineQuery.BorderStyle=BorderStyle.None;onlineQuery.BackColor=queryFrame.BackColor;onlineQuery.ForeColor=XdolfTheme.Text;onlineQuery.Font=new Font("Segoe UI",10.5f);onlineQuery.Multiline=false;
  queryGlyph=new Label{Text="\uE721",BackColor=queryFrame.BackColor,ForeColor=Color.FromArgb(211,225,238),TextAlign=ContentAlignment.MiddleCenter,Font=UiPaint.IconFont(16),Cursor=Cursors.IBeam};queryGlyph.Click+=delegate{onlineQuery.Focus();};queryFrame.Controls.Add(queryGlyph);queryGlyph.BringToFront();
  queryFrame.SetBounds(17,14,465,38);LayoutExtendedVisuals();queryFrame.BringToFront();
 }
 void LayoutExtendedVisuals(){
  if(queryFrame!=null){
   int w=Math.Max(120,queryFrame.Width);
   onlineQuery.SetBounds(12,9,Math.Max(70,w-60),22);
   if(queryGlyph!=null)queryGlyph.SetBounds(w-40,5,32,28);
  }
  FitLastFileColumn();
 }
 void ConfigureFileList(){
  files.OwnerDraw=true;
  files.DrawColumnHeader+=delegate(object s,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Color.FromArgb(12,24,34)))e.Graphics.FillRectangle(b,e.Bounds);using(var p=new Pen(Color.FromArgb(39,58,73)))e.Graphics.DrawLine(p,e.Bounds.Right-1,e.Bounds.Top,e.Bounds.Right-1,e.Bounds.Bottom);TextRenderer.DrawText(e.Graphics,e.Header.Text,new Font("Segoe UI",8.5f,FontStyle.Bold),new Rectangle(e.Bounds.X+9,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Color.FromArgb(184,202,219),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);};
  files.DrawItem+=delegate(object s,DrawListViewItemEventArgs e){};
  files.DrawSubItem+=delegate(object s,DrawListViewSubItemEventArgs e){bool selected=e.Item.Selected;Color bg=selected?Color.FromArgb(24,64,105):Color.FromArgb(8,17,25);using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,files.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),selected?Color.White:XdolfTheme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);};
  NativeDarkScroll.Apply(files);
  collectionView.Layout+=delegate{FitLastFileColumn();};
  files.ColumnWidthChanged+=delegate(object s,ColumnWidthChangedEventArgs e){if(e.ColumnIndex!=files.Columns.Count-1)FitLastFileColumn();};
  FitLastFileColumn();
 }
 void FitLastFileColumn(){
  if(fittingFileColumns||files.IsDisposed||files.Columns.Count<2||files.ClientSize.Width<=0)return;
  fittingFileColumns=true;
  try{
   int last=files.Columns.Count-1,fixedWidth=0;for(int i=0;i<last;i++)fixedWidth+=files.Columns[i].Width;
   int desired=Math.Max(220,files.ClientSize.Width-fixedWidth-2);
   if(files.Columns[last].Width!=desired)files.Columns[last].Width=desired;
   NativeDarkScroll.Apply(files);
  }catch{}finally{fittingFileColumns=false;}
 }
}
}
