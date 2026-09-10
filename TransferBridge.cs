using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace VideoShelf {
static class TransferBridge {
 static string FindHost(){
  string baseDir=AppDomain.CurrentDomain.BaseDirectory;
  string[] candidates={
   Path.Combine(baseDir,"TransferHost","VideoShelf.TransferHost.exe"),
   Path.Combine(baseDir,"TransferHostRuntime","VideoShelf.TransferHost.exe"),
   Path.Combine(baseDir,"VideoShelf.TransferHost.exe"),
   Path.Combine(baseDir,"TransferHost","bin","publish","VideoShelf.TransferHost.exe")
  };
  foreach(string path in candidates)if(File.Exists(path))return path;
  return "";
 }
 static string Q(string value){return "\""+(value??"").Replace("\"","\\\"")+"\"";}
 static bool LaunchHost(IWin32Window owner,string arguments){
  string host=FindHost();
  if(host.Length==0){
   MessageBox.Show(owner,"The VideoShelf transfer runtime is not installed beside VideoShelf.exe.\n\nUse the complete VideoShelf Windows package, which includes the TransferHost folder.","VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Information);
   return false;
  }
  try{
   Process.Start(new ProcessStartInfo(host,arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(host)});
   return true;
  }catch(Exception ex){
   MessageBox.Show(owner,"Could not start the VideoShelf transfer runtime.\n\n"+ex.Message,"VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Error);
   return false;
  }
 }
 public static bool Stream(IWin32Window owner,OnlineResult result){
  if(result==null||string.IsNullOrWhiteSpace(result.Link)){MessageBox.Show(owner,"This result does not contain a torrent/magnet link that can be streamed.","VideoShelf");return false;}
  return LaunchHost(owner,"stream --source "+Q(result.Link)+" --title "+Q(result.Title));
 }
 public static bool Download(IWin32Window owner,OnlineResult result,string suggestedFolder){
  if(result==null||string.IsNullOrWhiteSpace(result.Link)){MessageBox.Show(owner,"This result does not contain a torrent/magnet link that can be downloaded.","VideoShelf");return false;}
  using(var dialog=new FolderBrowserDialog{Description="Choose where VideoShelf should download this torrent",ShowNewFolderButton=true}){
   if(!string.IsNullOrWhiteSpace(suggestedFolder)&&Directory.Exists(suggestedFolder))dialog.SelectedPath=suggestedFolder;
   if(dialog.ShowDialog(owner)!=DialogResult.OK)return false;
   return LaunchHost(owner,"download --source "+Q(result.Link)+" --destination "+Q(dialog.SelectedPath)+" --title "+Q(result.Title));
  }
 }
}
}
