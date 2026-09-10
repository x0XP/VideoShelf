using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VideoShelf {
sealed partial class Shelf {
 async void LoadRoot(string path){
  portraitScan.Cancel();portraitScan.Dispose();portraitScan=new System.Threading.CancellationTokenSource();onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;ResetOnlineThumbnailImages();fetchingPortraits=false;onlineSearching=false;
  int token=++generation; current=null;root=path;onlineMode=false;back.Visible=false;detail.Visible=false;cards.Visible=true;ClearSearch();SetSort(false);ClearCards();foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();people.Clear();onlineResults.Clear();onlineError="";onlineQueryFor=""; title.Text="";SetHeader(false);subtitle.Text=root;status.Text="Loading portraits…";
  int errors=0; List<Person> loaded;
  try { loaded=await Task.Run(()=> {var list=new List<Person>();foreach(string dir in Directory.GetDirectories(path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)){ try{if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;}catch{errors++;continue;}var p=new Person{Path=dir,Name=Path.GetFileName(dir)};list.Add(p);}return list;}); }
  catch(Exception ex){if(token==generation&&!IsDisposed)status.Text="Cannot read folder: "+ex.Message;return;}
  if(token!=generation||IsDisposed){foreach(var p in loaded)if(p.Photo!=null)p.Photo.Dispose();return;}people=loaded;skipped=errors;Render();try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(settings,root);}catch{} FetchPortraits(people.ToArray(),portraitScan.Token);
 }
 async void FetchPortraits(Person[] batch,System.Threading.CancellationToken ct){
  fetchingPortraits=true;if(current==null)Render();
  try{foreach(var person in batch){ct.ThrowIfCancellationRequested();int revision=person.PhotoVersion;
   using(var result=await PortraitLookup.Find(person.Name,ct)){
    if(ct.IsCancellationRequested||IsDisposed)return;if(revision!=person.PhotoVersion)continue;
    ApplyPortrait(person,result);
   }
  }}catch(OperationCanceledException){}finally{if(!ct.IsCancellationRequested&&!IsDisposed){fetchingPortraits=false;if(current==null)Render();}}
 }
 void ApplyPortrait(Person person,PortraitResult result){
  if(result.Photo!=null&&!result.FromCache)try{PortraitLookup.Store(person.Name,result.Photo,result.Source);}catch{}
  if(person.Photo!=null)person.Photo.Dispose();person.Photo=result.Photo;result.Photo=null;person.PhotoSource=result.Source;person.PhotoError=result.Error;
  foreach(Control c in cards.Controls){var card=c as Portrait;if(card!=null&&card.Person==person){card.Invalidate();tips.SetToolTip(card,person.Name+"\n"+(person.PhotoError.Length>0?person.PhotoError:"Right-click for portrait options"));}}
 }
 void AttachPhotoMenu(Portrait card){
  var menu=new ContextMenuStrip();card.ContextMenuStrip=menu;card.Disposed+=delegate{menu.Dispose();};
  menu.Items.Add("Search images in browser",null,delegate{Launch(PortraitLookup.SearchUrl(card.Person.Name));});
  menu.Items.Add("Choose image…",null,delegate{
   using(var dialog=new OpenFileDialog{Filter="Pictures|*.jpg;*.jpeg;*.png",Title="Choose portrait for "+card.Person.Name})if(dialog.ShowDialog(this)==DialogResult.OK){
    try{using(var result=PortraitLookup.Local(card.Person.Name,dialog.FileName)){card.Person.PhotoVersion++;ApplyPortrait(card.Person,result);}}catch(Exception ex){MessageBox.Show(this,ex.Message,"Portrait unavailable");}
   }
  });
  menu.Items.Add("Retry automatic lookup",null,async delegate{
   Person person=card.Person;int revision=++person.PhotoVersion;var ct=portraitScan.Token;
   try{PortraitLookup.Forget(person.Name);using(var result=await PortraitLookup.Find(person.Name,ct)){if(!ct.IsCancellationRequested&&!IsDisposed&&revision==person.PhotoVersion)ApplyPortrait(person,result);}}
   catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,"Portrait unavailable");}
  });
  menu.Items.Add("View image source",null,delegate{Uri uri;if(Uri.TryCreate(card.Person.PhotoSource,UriKind.Absolute,out uri)&&uri.Scheme=="https")Launch(uri.AbsoluteUri);else status.Text="This portrait has no web source (it may be a chosen local image).";});
 }
 async void OpenPerson(Person p){
  onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;ResetOnlineThumbnailImages();onlineSearching=false;onlineResults.Clear();onlineError="";onlineQueryFor="";onlineQuery.Text=p.Name;
  int token=++generation;current=p;onlineMode=false;ClearSearch();SetSort(false);back.Visible=true;cards.Visible=false;detail.Visible=true;localDetail.Visible=true;onlineDetail.Visible=false;localDetail.BringToFront();title.Text=p.Name;SetHeader(true);subtitle.Text=p.Path;videos.Clear();files.Items.Clear();status.Text="Finding videos…";
  int errors=0; var found=await Task.Run(()=>{var list=new List<Video>();var pending=new Stack<string>();pending.Push(p.Path);while(pending.Count>0){var dir=pending.Pop();try{foreach(var child in Directory.GetDirectories(dir))try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch{errors++;}foreach(var f in Directory.GetFiles(dir)){if(!new[]{".mp4",".mkv",".avi",".mov",".wmv",".webm",".m4v",".mpg",".mpeg",".ts",".mts",".m2ts",".3gp",".flv",".vob"}.Contains(Path.GetExtension(f).ToLowerInvariant()))continue;try{var info=new FileInfo(f);list.Add(new Video{Path=f,Name=info.Name,Size=info.Length,Modified=info.LastWriteTime,Relative=dir.Length==p.Path.Length?"—":dir.Substring(p.Path.Length).TrimStart(Path.DirectorySeparatorChar)});}catch{errors++;}}}catch{errors++;}}return list;});
  if(token!=generation||IsDisposed)return;videos=found;skipped=errors;Render();animation.Stop();files.Dock=DockStyle.None;files.SetBounds(0,97,detail.ClientSize.Width,Math.Max(1,detail.ClientSize.Height-49));files.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;animation.Start();
  onlineSettings=OnlineSettings.Load();if(onlineSettings.Configured&&onlineSettings.AutoSearch)SearchOnline(p.Name,false);
 }
 void ShowPeople(){generation++;onlineScan.Cancel();thumbnailScan.Cancel();onlineSearching=false;thumbnailsLoading=false;animation.Stop();files.Dock=DockStyle.Fill;current=null;onlineMode=false;ClearSearch();SetSort(false);detail.Visible=false;cards.Visible=true;back.Visible=false;title.Text="";SetHeader(false);subtitle.Text=root;skipped=0;Render();}
 void ShowLocal(){if(current==null)return;thumbnailScan.Cancel();thumbnailsLoading=false;onlineMode=false;ClearSearch();SetSort(false);onlineDetail.Visible=false;localDetail.Visible=true;localDetail.BringToFront();subtitle.Text=current.Path;Render();}
}
}
