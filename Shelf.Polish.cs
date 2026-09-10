using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool finalPolishApplied;
 MockupFilter sourceVisual,categoryVisual,resolutionVisual,sortVisual;
 InspectorMetadataView inspectorMetadataVisual;

 void ApplyFinalPolish(){
  if(finalPolishApplied)return;
  finalPolishApplied=true;

  sourceVisual=CreateFilter(sourceFilter);
  categoryVisual=CreateFilter(categoryFilter);
  resolutionVisual=CreateFilter(resolution);
  sortVisual=CreateFilter(homeSort);

  sourceFilter.Visible=false;categoryFilter.Visible=false;resolution.Visible=false;homeSort.Visible=false;

  inspectorMetadataVisual=new InspectorMetadataView();
  inspectorMetadataVisual.RawText=inspectorMeta.Text;
  inspector.Controls.Add(inspectorMetadataVisual);
  inspectorMeta.TextChanged+=delegate{if(inspectorMetadataVisual!=null)inspectorMetadataVisual.RawText=inspectorMeta.Text;};
  inspectorMeta.Visible=false;

  inspectorTitle.AutoEllipsis=false;
  inspectorTitle.TextAlign=ContentAlignment.TopLeft;

  files.Resize+=delegate{FillLastFileColumn();};
  FillLastFileColumn();
  LayoutFinalPolish();
 }
 MockupFilter CreateFilter(ComboBox combo){
  var visual=new MockupFilter(combo);
  Control parent=combo.Parent;
  if(parent!=null){parent.Controls.Add(visual);visual.BringToFront();}
  return visual;
 }
 void LayoutFinalPolish(){
  if(!finalPolishApplied)return;
  if(sourceVisual!=null)sourceVisual.SetBounds(496,13,150,40);
  if(categoryVisual!=null)categoryVisual.SetBounds(658,13,125,40);
  if(resolutionVisual!=null)resolutionVisual.SetBounds(795,13,140,40);
  if(sortVisual!=null)sortVisual.SetBounds(700,101,160,32);
  if(inspectorMetadataVisual!=null){
   int w=Math.Max(0,inspector.ClientSize.Width-26);
   int h=downloadVisual==null?220:Math.Max(105,downloadVisual.Top-360);
   inspectorMetadataVisual.SetBounds(13,345,w,h);
   inspectorMetadataVisual.BringToFront();
  }
  FillLastFileColumn();
 }
 void FillLastFileColumn(){
  try{
   if(files.Columns.Count<5||files.ClientSize.Width<=0)return;
   int used=0;for(int i=0;i<files.Columns.Count-1;i++)used+=files.Columns[i].Width;
   files.Columns[files.Columns.Count-1].Width=Math.Max(160,files.ClientSize.Width-used-4);
  }catch{}
 }
}
}
