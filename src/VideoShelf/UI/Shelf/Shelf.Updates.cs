using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoShelf {
sealed partial class Shelf {
 readonly Label updateStatus=new Label();
 readonly Button updateButton=new Button();
 CancellationTokenSource updateCts=new CancellationTokenSource();
 UpdateInfo availableUpdate;
 bool updateBusy;

 internal void InitializeUpdater(){
  UpdateDisplayedVersion();
  BuildUpdateSettings();
  Shown+=async delegate{await CheckForUpdatesAsync(false);};
  FormClosed+=delegate{try{updateCts.Cancel();updateCts.Dispose();}catch{}};
 }

 void UpdateDisplayedVersion(){
  Text="VideoShelf "+AppVersion.Current;
  foreach(Control control in Controls){UpdateVersionLabels(control);}
 }
 void UpdateVersionLabels(Control rootControl){
  var label=rootControl as Label;
  if(label!=null&&label.Text.StartsWith("VideoShelf v",StringComparison.OrdinalIgnoreCase))label.Text="VideoShelf v"+AppVersion.Display;
  foreach(Control child in rootControl.Controls)UpdateVersionLabels(child);
 }

 void BuildUpdateSettings(){
  var box=new Panel{Left=24,Top=312,Width=760,Height=166,BackColor=XdolfTheme.PanelRaised};
  box.Paint+=delegate(object sender,PaintEventArgs e){using(var p=new Pen(XdolfTheme.Outline))e.Graphics.DrawRectangle(p,0,0,box.Width-1,box.Height-1);};
  var heading=new Label{Text="Application updates",Left=20,Top=19,Width=260,Height=24,ForeColor=Color.White,Font=new Font("Segoe UI",10.5f,FontStyle.Bold)};
  var version=new Label{Text="Installed version: v"+AppVersion.Current,Left=20,Top=48,Width=330,Height=22,ForeColor=XdolfTheme.Muted};
  updateStatus.SetBounds(20,76,710,30);updateStatus.ForeColor=XdolfTheme.Muted;updateStatus.Text="Stable updates are delivered through GitHub Releases.";
  Style(updateButton,"Check for updates",20,113,154);updateButton.Height=36;
  updateButton.Click+=async delegate{if(availableUpdate!=null&&!updateBusy)await PromptUpdateAsync(availableUpdate);else await CheckForUpdatesAsync(true);};
  box.Controls.AddRange(new Control[]{heading,version,updateStatus,updateButton});
  settingsView.Controls.Add(box);
 }

 async Task CheckForUpdatesAsync(bool interactive){
  if(updateBusy)return;
  UpdateInfo promptInfo=null;
  updateBusy=true;availableUpdate=null;updateButton.Enabled=false;updateButton.Text="Checking...";updateStatus.Text="Checking the latest stable release...";
  try{
   var info=await UpdateService.CheckForUpdateAsync(updateCts.Token);
   if(IsDisposed)return;
   if(info==null){updateStatus.Text="You're up to date — v"+AppVersion.Current;updateButton.Text="Check for updates";if(interactive)MessageBox.Show(this,"VideoShelf v"+AppVersion.Current+" is the latest stable release.","VideoShelf updates",MessageBoxButtons.OK,MessageBoxIcon.Information);}
   else{availableUpdate=info;promptInfo=info;updateStatus.Text="VideoShelf v"+info.VersionText+" is available.";updateButton.Text="Install update";}
  }catch(OperationCanceledException){}
  catch(Exception ex){if(!IsDisposed){updateStatus.Text="Update check unavailable. You can try again later.";updateButton.Text="Check for updates";if(interactive)MessageBox.Show(this,"VideoShelf could not check for updates.\n\n"+ex.Message,"VideoShelf updates",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
  finally{if(!IsDisposed){updateBusy=false;updateButton.Enabled=true;}}
  if(promptInfo!=null&&!IsDisposed)await PromptUpdateAsync(promptInfo);
 }

 static string FormatUserReleaseNotes(string raw){
  if(string.IsNullOrWhiteSpace(raw))return "No release notes were provided.";
  var items=new System.Collections.Generic.List<string>();
  foreach(string rawLine in raw.Replace("\r","").Split('\n')){
   string line=(rawLine??"").Trim();
   if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;
   if(line.StartsWith("Passed ",StringComparison.OrdinalIgnoreCase)||line.StartsWith("This release ",StringComparison.OrdinalIgnoreCase))continue;
   if(line.StartsWith("- ",StringComparison.Ordinal))line="• "+line.Substring(2).Trim();
   else if(items.Count>0)continue;
   items.Add(line);
   if(items.Count>=4)break;
  }
  string notes=string.Join("\n",items.ToArray());
  if(notes.Length==0)notes="See the release page for details.";
  if(notes.Length>550)notes=notes.Substring(0,550).TrimEnd()+"…";
  return notes;
 }

 async Task PromptUpdateAsync(UpdateInfo info){
  if(info==null||updateBusy)return;
  string notes=FormatUserReleaseNotes(info.Notes);
  string message="VideoShelf v"+info.VersionText+" is available.\n\nWhat's new:\n"+notes+"\n\nDownload and install this update now?";
  if(MessageBox.Show(this,message,"VideoShelf update available",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)await InstallUpdateAsync(info);
 }

 async Task InstallUpdateAsync(UpdateInfo info){
  if(info==null||updateBusy)return;
  updateBusy=true;updateButton.Enabled=false;updateButton.Text="Downloading...";
  using(var progressWindow=new UpdateProgressForm(info)){
   progressWindow.Show(this);progressWindow.BringToFront();progressWindow.SetDownloadProgress(0);
   try{
    var progress=new Progress<int>(pct=>{
     if(IsDisposed)return;
     if(pct<0){updateStatus.Text="Verifying VideoShelf v"+info.VersionText+"...";progressWindow.SetVerifying();}
     else if(pct>=100){updateStatus.Text="VideoShelf v"+info.VersionText+" downloaded and verified.";progressWindow.SetVerified();}
     else{updateStatus.Text="Downloading VideoShelf v"+info.VersionText+" — "+pct+"%";progressWindow.SetDownloadProgress(pct);}
    });
    string installer=await UpdateService.DownloadInstallerAsync(info,progress,updateCts.Token);
    if(IsDisposed)return;
    updateStatus.Text="Verified. Installing VideoShelf v"+info.VersionText+"...";
    progressWindow.SetInstalling();progressWindow.BringToFront();
    UpdateService.LaunchInstallerAndRestart(installer);
    Application.Exit();
   }catch(OperationCanceledException){}
   catch(Exception ex){if(!IsDisposed){updateStatus.Text="The update was not installed.";progressWindow.SetFailure("The current installation was not changed.");progressWindow.BringToFront();MessageBox.Show(progressWindow,"VideoShelf could not install the update. The current installation has not been changed.\n\n"+ex.Message,"VideoShelf updates",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
   finally{if(!IsDisposed){updateBusy=false;updateButton.Enabled=true;updateButton.Text=availableUpdate==null?"Check for updates":"Install update";}}
  }
 }
}
}
