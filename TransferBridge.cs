using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace VideoShelf {
sealed class TransferActivity {
 public int Id; public string Mode="",Title="",Destination=""; public DateTime Started; public Process Process;
}
static class TransferBridge {
 static readonly object gate=new object();static readonly List<TransferActivity> active=new List<TransferActivity>();static int nextId;
 public static int ActiveDownloads {get{lock(gate)return active.Count(a=>a.Mode=="download"&&a.Process!=null&&!a.Process.HasExited);}}
 public static int ActiveStreams {get{lock(gate)return active.Count(a=>a.Mode=="stream"&&a.Process!=null&&!a.Process.HasExited);}}
 public static List<TransferActivity> Snapshot(string mode){lock(gate){Prune();return active.Where(a=>a.Mode.Equals(mode,StringComparison.OrdinalIgnoreCase)).OrderByDescending(a=>a.Started).Select(a=>new TransferActivity{Id=a.Id,Mode=a.Mode,Title=a.Title,Destination=a.Destination,Started=a.Started,Process=a.Process}).ToList();}}
 static void Prune(){active.RemoveAll(a=>a.Process==null||a.Process.HasExited);}
 static string FindHost(){string baseDir=AppDomain.CurrentDomain.BaseDirectory;string[] candidates={Path.Combine(baseDir,"TransferHost","VideoShelf.TransferHost.exe"),Path.Combine(baseDir,"TransferHostRuntime","VideoShelf.TransferHost.exe"),Path.Combine(baseDir,"VideoShelf.TransferHost.exe"),Path.Combine(baseDir,"TransferHost","bin","publish","VideoShelf.TransferHost.exe")};foreach(string path in candidates)if(File.Exists(path))return path;return "";}
 static string Q(string value){return "\""+(value??"").Replace("\"","\\\"")+"\"";}
 static bool LaunchHost(IWin32Window owner,string arguments,string mode,string title,string destination){
  string host=FindHost();if(host.Length==0){MessageBox.Show(owner,"The VideoShelf transfer runtime is not installed beside VideoShelf.exe.\n\nUse the complete VideoShelf Windows package, which includes the TransferHost folder.","VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
  try{var process=Process.Start(new ProcessStartInfo(host,arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(host)});if(process==null)throw new InvalidOperationException("The transfer runtime did not start.");process.EnableRaisingEvents=true;if(mode=="download"||mode=="stream"){var item=new TransferActivity{Id=System.Threading.Interlocked.Increment(ref nextId),Mode=mode,Title=title??"Torrent",Destination=destination??"",Started=DateTime.Now,Process=process};lock(gate){Prune();active.Add(item);}process.Exited+=delegate{lock(gate)Prune();};}return true;}catch(Exception ex){MessageBox.Show(owner,"Could not start the VideoShelf transfer runtime.\n\n"+ex.Message,"VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
 }
 public static bool Stream(IWin32Window owner,OnlineResult result){if(result==null||string.IsNullOrWhiteSpace(result.Link)){MessageBox.Show(owner,"This result does not contain a torrent/magnet link that can be streamed.","VideoShelf");return false;}return LaunchHost(owner,"stream --source "+Q(result.Link)+" --title "+Q(result.Title),"stream",result.Title,"");}
 public static bool Download(IWin32Window owner,OnlineResult result,string suggestedFolder){
  if(result==null||string.IsNullOrWhiteSpace(result.Link)){MessageBox.Show(owner,"This result does not contain a torrent/magnet link that can be downloaded.","VideoShelf");return false;}
  using(var dialog=new FolderBrowserDialog{Description="Choose where VideoShelf should download this torrent",ShowNewFolderButton=true}){if(!string.IsNullOrWhiteSpace(suggestedFolder)&&Directory.Exists(suggestedFolder))dialog.SelectedPath=suggestedFolder;if(dialog.ShowDialog(owner)!=DialogResult.OK)return false;return LaunchHost(owner,"download --source "+Q(result.Link)+" --destination "+Q(dialog.SelectedPath)+" --title "+Q(result.Title),"download",result.Title,dialog.SelectedPath);}
 }
 public static bool ViewFiles(IWin32Window owner,OnlineResult result){if(result==null||string.IsNullOrWhiteSpace(result.Link)){MessageBox.Show(owner,"This result does not contain torrent metadata that can be inspected.","VideoShelf");return false;}return LaunchHost(owner,"files --source "+Q(result.Link)+" --title "+Q(result.Title),"files",result.Title,"");}
}
}
