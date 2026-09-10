using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VideoShelf {
sealed partial class Shelf {
 async void LoadRoot(string path){
  portraitScan.Cancel();portraitScan.Dispose();portraitScan=new System.Threading.CancellationTokenSource();onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;fetchingPortraits=false;onlineSearching=false;selectedOnline=null;
  int token=++generation;current=null;root=path;onlineResults.Clear();onlineError="";onlineQueryFor="";homePath.Text=root;settingsPath.Text=root;addFolder.Enabled=Directory.Exists(root);SetStatus("Loading library…",root);
  int errors=0;List<Person> loaded;
  try{loaded=await Task.Run(()=>{var list=new List<Person>();foreach(string dir in Directory.GetDirectories(path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)){try{if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;}catch{errors++;continue;}list.Add(new Person{Path=dir,Name=FolderNaming.DisplayName(dir)});}return list;});}
  catch(Exception ex){if(token==generation&&!IsDisposed)SetStatus("Library unavailable",ex.Message);return;}
  if(token!=generation||IsDisposed){foreach(var p in loaded)if(p.Photo!=null)p.Photo.Dispose();return;}
  foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();people=loaded;skipped=errors;RenderHome();try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(settings,root);}catch{}
  if(section!=ShellSection.Home&&section!=ShellSection.Collections)ShowCollections();FetchPortraits(people.ToArray(),portraitScan.Token);
 }
 async void FetchPortraits(Person[] batch,System.Threading.CancellationToken ct){
  fetchingPortraits=true;RenderHome();try{foreach(var person in batch){ct.ThrowIfCancellationRequested();int revision=person.PhotoVersion;using(var result=await PortraitLookup.Find(person.Name,ct)){if(ct.IsCancellationRequested||IsDisposed)return;if(revision!=person.PhotoVersion)continue;ApplyPortrait(person,result);}}}catch(OperationCanceledException){}finally{if(!ct.IsCancellationRequested&&!IsDisposed){fetchingPortraits=false;RenderHome();}}
 }
 void ApplyPortrait(Person person,PortraitResult result){
  if(result.Photo!=null&&!result.FromCache)try{PortraitLookup.Store(person.Name,result.Photo,result.Source);}catch{}
  if(person.Photo!=null)person.Photo.Dispose();person.Photo=result.Photo;result.Photo=null;person.PhotoSource=result.Source;person.PhotoError=result.Error;
  foreach(Control c in cards.Controls){var card=c as Portrait;if(card!=null&&card.Person==person){card.Invalidate();tips.SetToolTip(card,person.Name+"\n"+(person.PhotoError.Length>0?person.PhotoError:"Right-click for artwork options"));}}
 }
 void AttachPhotoMenu(Portrait card){
  var menu=new ContextMenuStrip();menu.BackColor=XdolfTheme.PanelRaised;menu.ForeColor=XdolfTheme.Text;card.ContextMenuStrip=menu;card.Disposed+=delegate{menu.Dispose();};
  menu.Items.Add("Search images in browser",null,delegate{Launch(PortraitLookup.SearchUrl(card.Person.Name));});
  menu.Items.Add("Choose image…",null,delegate{using(var dialog=new OpenFileDialog{Filter="Pictures|*.jpg;*.jpeg;*.png",Title="Choose artwork for "+card.Person.Name})if(dialog.ShowDialog(this)==DialogResult.OK){try{using(var result=PortraitLookup.Local(card.Person.Name,dialog.FileName)){card.Person.PhotoVersion++;ApplyPortrait(card.Person,result);}}catch(Exception ex){MessageBox.Show(this,ex.Message,"Artwork unavailable");}}});
  menu.Items.Add("Retry automatic lookup",null,async delegate{Person person=card.Person;int revision=++person.PhotoVersion;var ct=portraitScan.Token;try{PortraitLookup.Forget(person.Name);using(var result=await PortraitLookup.Find(person.Name,ct)){if(!ct.IsCancellationRequested&&!IsDisposed&&revision==person.PhotoVersion)ApplyPortrait(person,result);}}catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,"Artwork unavailable");}});
  menu.Items.Add("View image source",null,delegate{Uri uri;if(Uri.TryCreate(card.Person.PhotoSource,UriKind.Absolute,out uri)&&uri.Scheme=="https")Launch(uri.AbsoluteUri);else SetStatus("Artwork","This collection has no web image source.");});
 }
 void AddLibraryFolder(){
  if(root.Length==0||!Directory.Exists(root)){SetStatus("Library","Choose a library folder before adding a collection.");return;}
  using(var dialog=new AddFolderDialog())if(dialog.ShowDialog(this)==DialogResult.OK){try{string path=FolderNaming.CreateCollection(root,dialog.FolderName);string display=FolderNaming.DisplayName(path);SetStatus("Added collection",display);LoadRoot(root);}catch(Exception ex){MessageBox.Show(this,"Could not create the folder.\n\n"+ex.Message,"VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
 }
 async void OpenPerson(Person p){
  onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;onlineSearching=false;selectedOnline=null;onlineQuery.Text=p.Name;
  int token=++generation;current=p;collectionTitle.Text=p.Name;collectionPath.Text=p.Path;ShowSection(collectionView,ShellSection.Collections);navCollections.SetActive(true);SetStatus("Scanning collection…","Finding local videos");videos.Clear();files.Items.Clear();
  int errors=0;var found=await Task.Run(()=>{var list=new List<Video>();var pending=new Stack<string>();pending.Push(p.Path);while(pending.Count>0){var dir=pending.Pop();try{foreach(var child in Directory.GetDirectories(dir))try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch{errors++;}foreach(var f in Directory.GetFiles(dir)){if(!new[]{".mp4",".mkv",".avi",".mov",".wmv",".webm",".m4v",".mpg",".mpeg",".ts",".mts",".m2ts",".3gp",".flv",".vob"}.Contains(Path.GetExtension(f).ToLowerInvariant()))continue;try{var info=new FileInfo(f);list.Add(new Video{Path=f,Name=info.Name,Size=info.Length,Modified=info.LastWriteTime,Relative=dir.Length==p.Path.Length?"—":dir.Substring(p.Path.Length).TrimStart(Path.DirectorySeparatorChar)});}catch{errors++;}}}catch{errors++;}}return list;});
  if(token!=generation||IsDisposed)return;videos=found;skipped=errors;RenderLocal();onlineSettings=OnlineSettings.Load();if(onlineSettings.Configured&&onlineSettings.AutoSearch)SearchOnline(p.Name,false);
 }
 void RenderHome(){
  if(cards.IsDisposed)return;string q=libraryFilter.Text.Trim();IEnumerable<Person> selected=people.Where(p=>p.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0);selected=homeSort.SelectedIndex==1?selected.OrderByDescending(p=>p.Name,StringComparer.OrdinalIgnoreCase):selected.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase);
  cards.SuspendLayout();ClearCards();foreach(var p in selected){var card=new Portrait(p);card.Click+=delegate{OpenPerson(card.Person);};AttachPhotoMenu(card);tips.SetToolTip(card,p.Name+(p.PhotoError.Length>0?"\n"+p.PhotoError:"\nRight-click for artwork options"));cards.Controls.Add(card);}cards.ResumeLayout();
  string left=people.Count==0?"No collections":people.Count+" collection"+(people.Count==1?"":"s");if(fetchingPortraits)left+=" • loading artwork";if(skipped>0)left+=" • unreadable items skipped";SetStatus(left,root.Length==0?"Choose a library folder to begin.":root);
 }
 void RenderLocal(){
  files.BeginUpdate();files.Items.Clear();foreach(var v in videos.OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase)){var item=new ListViewItem(new[]{v.Name,Path.GetExtension(v.Path).TrimStart('.').ToUpperInvariant(),SizeText(v.Size),v.Modified.ToString("dd MMM yyyy HH:mm"),v.Relative});item.Tag=v;files.Items.Add(item);}files.EndUpdate();SetStatus(files.Items.Count+" local video"+(files.Items.Count==1?"":"s"),files.Items.Count==0?"No local videos found. Use Find online to search metadata.":"Double-click a video to play it.");
 }
 void ClearCards(){while(cards.Controls.Count>0)cards.Controls[0].Dispose();}
 void Play(){if(files.SelectedItems.Count>0)Launch(((Video)files.SelectedItems[0].Tag).Path);else SetStatus("Select a video","Choose a local video first, then Play selected.");}
 void ShowPeople(){ShowCollections();}
}
}
