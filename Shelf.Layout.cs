using System;
using System.Linq;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 protected override void OnShown(EventArgs e){
  base.OnShown(e);
  FixShellZOrder();
 }
 protected override void OnResize(EventArgs e){
  base.OnResize(e);
  if(IsHandleCreated)FixShellZOrder();
 }
 void FixShellZOrder(){
  if(IsDisposed)return;
  body.SendToBack();
  Control results=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c.Dock==DockStyle.Fill);
  Control filters=searchView.Controls.Cast<Control>().FirstOrDefault(c=>c!=inspector&&c.Dock==DockStyle.Top);
  if(results!=null)results.SendToBack();
  if(inspector!=null)inspector.BringToFront();
  if(filters!=null)filters.BringToFront();
  PerformLayout();
  searchView.PerformLayout();
 }
}
}
