using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 bool finalPolishApplied,homePolishApplied;
 MockupFilter sourceVisual,categoryVisual,resolutionVisual,languageVisual,sortVisual;
 InspectorMetadataView inspectorMetadataVisual;
 SearchScrollRail onlineRail;
 const int SearchFilterHeight=40;

 void ApplyHomePolish(){
  if(homePolishApplied)return;
  homePolishApplied=true;
  sortVisual=CreateFilter(homeSort);homeSort.Visible=false;
  LayoutHomePolish();
 }
 void LayoutHomePolish(){if(sortVisual!=null){sortVisual.Bounds=homeSort.Bounds;sortVisual.BringToFront();}}

 void ApplyFinalPolish(){
  if(finalPolishApplied)return;
  finalPolishApplied=true;

  sourceVisual=CreateFilter(sourceFilter);
  categoryVisual=CreateFilter(categoryFilter);
  resolutionVisual=CreateFilter(resolution);
  languageVisual=CreateFilter(languageFilter);
  sourceFilter.Visible=false;categoryFilter.Visible=false;resolution.Visible=false;languageFilter.Visible=false;

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
 void LayoutSearchFilter(MockupFilter visual,ComboBox combo){
  if(visual==null||combo==null)return;
  visual.SetBounds(combo.Left,combo.Top,combo.Width,SearchFilterHeight);
  visual.BringToFront();
 }
 void LayoutFinalPolish(){
  if(!finalPolishApplied)return;
  LayoutSearchFilter(sourceVisual,sourceFilter);
  LayoutSearchFilter(categoryVisual,categoryFilter);
  LayoutSearchFilter(resolutionVisual,resolution);
  LayoutSearchFilter(languageVisual,languageFilter);
  if(inspectorMetadataVisual!=null){
   inspectorMetadataVisual.Bounds=inspectorMeta.Bounds;
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
