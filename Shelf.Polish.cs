using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool finalPolishApplied,homePolishApplied;
 MockupFilter sourceVisual,categoryVisual,resolutionVisual,sortVisual;
 InspectorMetadataView inspectorMetadataVisual;
 SearchScrollRail onlineRail;

 void ApplyHomePolish(){
  if(homePolishApplied)return;
  homePolishApplied=true;
  sortVisual=CreateFilter(homeSort);homeSort.Visible=false;
  LayoutHomePolish();
 }
 void LayoutHomePolish(){if(sortVisual!=null){sortVisual.SetBounds(700,101,160,32);sortVisual.BringToFront();}}

 void ApplyFinalPolish(){
  if(finalPolishApplied)return;
  finalPolishApplied=true;

  sourceVisual=CreateFilter(sourceFilter);
  categoryVisual=CreateFilter(categoryFilter);
  resolutionVisual=CreateFilter(resolution);
  sourceFilter.Visible=false;categoryFilter.Visible=false;resolution.Visible=false;

  inspectorMetadataVisual=new InspectorMetadataView();
  inspectorMetadataVisual.RawText=inspectorMeta.Text;
  inspector.Controls.Add(inspectorMetadataVisual);
  inspectorMeta.TextChanged+=delegate{if(inspectorMetadataVisual!=null)inspectorMetadataVisual.RawText=inspectorMeta.Text;};
  inspectorMeta.Visible=false;

  inspectorTitle.AutoEllipsis=false;
  inspectorTitle.TextAlign=ContentAlignment.TopLeft;

  NativeDarkScroll.Apply(onlineCards);
  if(onlineCards.Parent!=null){onlineRail=new SearchScrollRail(onlineCards);onlineCards.Parent.Controls.Add(onlineRail);onlineRail.BringToFront();}
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
  if(inspectorMetadataVisual!=null){
   int w=Math.Max(0,inspector.ClientSize.Width-26);
   int h=downloadVisual==null?220:Math.Max(105,downloadVisual.Top-371);
   inspectorMetadataVisual.SetBounds(13,356,w,h);
   inspectorMetadataVisual.BringToFront();
  }
  NativeDarkScroll.Apply(onlineCards);
  if(onlineRail!=null&&onlineCards.Parent==onlineRail.Parent){
   onlineRail.SetBounds(Math.Max(0,onlineCards.Right-18),onlineCards.Top,18,Math.Max(0,onlineCards.Height));
   onlineRail.BringToFront();
  }
 }
}
}
