using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoShelf {
sealed class UpdateProgressForm : Form {
 readonly Label heading=new Label(),detail=new Label(),percent=new Label();
 readonly Panel track=new Panel(),fill=new Panel();
 readonly string version;

 public UpdateProgressForm(UpdateInfo info){
  version=info==null?"":info.VersionText;
  Text="VideoShelf update";
  ClientSize=new Size(470,174);
  FormBorderStyle=FormBorderStyle.FixedDialog;
  MaximizeBox=false;MinimizeBox=false;ControlBox=false;ShowInTaskbar=false;
  StartPosition=FormStartPosition.CenterParent;
  BackColor=XdolfTheme.Background;
  ForeColor=Color.White;

  heading.SetBounds(24,22,420,30);heading.Text="Downloading update";heading.ForeColor=Color.White;heading.Font=new Font("Segoe UI",12f,FontStyle.Bold);
  detail.SetBounds(24,60,350,24);detail.Text="VideoShelf v"+version+" • preparing download…";detail.ForeColor=XdolfTheme.Muted;detail.Font=new Font("Segoe UI",9f);
  percent.SetBounds(382,60,62,24);percent.Text="0%";percent.TextAlign=ContentAlignment.MiddleRight;percent.ForeColor=Color.FromArgb(215,226,236);percent.Font=new Font("Segoe UI",9f,FontStyle.Bold);
  track.SetBounds(24,99,420,10);track.BackColor=Color.FromArgb(25,43,56);
  fill.SetBounds(0,0,0,10);fill.BackColor=XdolfTheme.AccentBlue;track.Controls.Add(fill);
  var note=new Label{Left=24,Top=124,Width=420,Height=28,Text="Keep VideoShelf open while the update is downloaded and verified.",ForeColor=XdolfTheme.Muted,Font=new Font("Segoe UI",8.5f)};
  Controls.AddRange(new Control[]{heading,detail,percent,track,note});
 }

 public void SetDownloadProgress(int value){
  int pct=Math.Max(0,Math.Min(100,value));
  heading.Text="Downloading update";
  detail.Text="VideoShelf v"+version+" • "+pct+"% downloaded";
  percent.Text=pct+"%";
  SetBar(pct);
 }
 public void SetVerifying(){
  heading.Text="Verifying update";
  detail.Text="Checking the downloaded installer integrity…";
  percent.Text="";
  SetBar(100);
 }
 public void SetVerified(){
  heading.Text="Update downloaded";
  detail.Text="VideoShelf v"+version+" was downloaded and verified.";
  percent.Text="100%";
  SetBar(100);
 }
 public void SetInstalling(){
  heading.Text="Installing update";
  detail.Text="VideoShelf will close briefly and reopen automatically.";
  percent.Text="";
  SetBar(100);
 }
 public void SetFailure(string message){
  heading.Text="Update failed";
  detail.Text=string.IsNullOrWhiteSpace(message)?"The current installation was not changed.":message;
  percent.Text="";
 }
 void SetBar(int pct){
  int width=(int)Math.Round(track.ClientSize.Width*(pct/100d));
  fill.Width=Math.Max(0,Math.Min(track.ClientSize.Width,width));
  fill.Invalidate();track.Invalidate();
 }
}
}
