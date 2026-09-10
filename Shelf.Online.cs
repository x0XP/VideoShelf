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
 void ShowOnline(){
  if(current==null)return;onlineMode=true;ClearSearch();SetSort(true);localDetail.Visible=false;onlineDetail.Visible=true;onlineDetail.BringToFront();subtitle.Text="Online results for "+current.Name+" • metadata only";onlineSettings=OnlineSettings.Load();
  if(onlineQuery.Text.Trim().Length==0)onlineQuery.Text=current.Name;
  if(!onlineSettings.Configured){onlineError="Configure an online Torznab source to search.";RenderOnline();return;}
  if(!onlineSearching&&(onlineResults.Count==0||!onlineQueryFor.Equals(onlineQuery.Text.Trim(),StringComparison.OrdinalIgnoreCase)))SearchOnline(onlineQuery.Text,true);else RenderOnline();
 }
 void ConfigureOnline(){
  using(var d=new OnlineSourceDialog(onlineSettings))if(d.ShowDialog(this)==DialogResult.OK&&d.Value!=null){onlineSettings=d.Value;try{onlineSettings.Save();}catch(Exception ex){MessageBox.Show(this,"Could not save online source settings.\n\n"+ex.Message,"VideoShelf");return;}onlineResults.Clear();onlineQueryFor="";onlineError="";if(onlineMode&&onlineSettings.Configured)SearchOnline(onlineQuery.Text,true);else if(onlineMode)RenderOnline();}
 }
 async void SearchOnline(string query,bool showStatus){
  onlineSettings=OnlineSettings.Load();if(!onlineSettings.Configured){onlineError="Configure an online Torznab source to search.";if(showStatus)RenderOnline();return;}
  query=query.Trim();if(query.Length==0){onlineError="Enter a search query.";if(showStatus)RenderOnline();return;}
  onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;var ct=onlineScan.Token;Person target=current;onlineSearching=true;onlineError="";onlineQueryFor=query;if(showStatus&&onlineMode)RenderOnline();
  try{
   var found=await TorznabSearch.Search(query,onlineSettings,ct);if(ct.IsCancellationRequested||IsDisposed||target!=current)return;onlineResults=found;
  }catch(OperationCanceledException){return;}catch(Exception ex){if(ct.IsCancellationRequested||IsDisposed||target!=current)return;onlineResults.Clear();onlineError=ex.Message;}
  finally{if(!ct.IsCancellationRequested&&!IsDisposed&&target==current){onlineSearching=false;if(onlineMode)RenderOnline();else Render();}}
 }
 void Render(){
  if(onlineMode){RenderOnline();return;}
  string q=search.Text.Trim();if(current==null){ClearCards();var selected=people.Where(p=>p.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0);selected=sort.SelectedIndex==1?selected.OrderByDescending(p=>p.Name,StringComparer.OrdinalIgnoreCase):selected.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase);cards.SuspendLayout();foreach(var p in selected){var card=new Portrait(p);card.Click+=delegate{OpenPerson(card.Person);};AttachPhotoMenu(card);tips.SetToolTip(card,p.Name+(p.PhotoError.Length>0?"\n"+p.PhotoError:"\nRight-click for portrait options"));cards.Controls.Add(card);}cards.ResumeLayout();status.Text=cards.Controls.Count+" people"+(cards.Controls.Count==0?" • No matching folders. Choose a parent folder containing people's folders.":" • Click a portrait to explore videos");}
  else{var selected=videos.Where(v=>v.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||v.Relative.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0);selected=sort.SelectedIndex==2?selected.OrderByDescending(v=>v.Modified):sort.SelectedIndex==1?selected.OrderByDescending(v=>v.Name,StringComparer.OrdinalIgnoreCase):selected.OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase);files.BeginUpdate();files.Items.Clear();foreach(var v in selected){var item=new ListViewItem(new[]{v.Name,Path.GetExtension(v.Path).TrimStart('.').ToUpperInvariant(),SizeText(v.Size),v.Modified.ToString("dd MMM yyyy HH:mm"),v.Relative});item.Tag=v;files.Items.Add(item);}files.EndUpdate();status.Text=files.Items.Count+" local videos"+(files.Items.Count==0?" • No matching videos in this folder or its subfolders.":" • Double-click a video to play");if(!onlineSearching&&onlineResults.Count>0)status.Text+=" • "+onlineResults.Count+" seeded online matches ready";else if(onlineSearching)status.Text+=" • Checking online metadata…";}
  if(skipped>0)status.Text+=" • Some unreadable items were skipped";if(current==null){if(fetchingPortraits)status.Text+=" • Finding portraits…";int missing=people.Count(p=>p.PhotoError.Length>0);if(missing>0)status.Text+=" • "+missing+" portraits unavailable (right-click for options)";}
 }
 void ResetOnlineThumbnailImages(){
  onlineThumbs.Images.Clear();using(Image placeholder=MakeOnlinePlaceholder())onlineThumbs.Images.Add("__placeholder",placeholder);
 }
 async void StartThumbnailLoading(IEnumerable<OnlineResult> displayed){
  if(thumbnailsLoading||thumbnailsPaused||onlineSearching||!onlineMode||IsDisposed)return;
  var pending=displayed.Where(r=>r.Seeders>0).GroupBy(r=>OnlineThumbnailLookup.CacheKey(r.Title),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).Where(r=>!thumbnailAttempted.Contains(OnlineThumbnailLookup.CacheKey(r.Title))&&!onlineThumbs.Images.ContainsKey(OnlineThumbnailLookup.CacheKey(r.Title))).Take(40).ToArray();
  if(pending.Length==0)return;
  thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();var ct=thumbnailScan.Token;Person target=current;thumbnailsLoading=true;if(onlineMode)status.Text+=" • Loading artwork only…";
  try{
   foreach(var row in pending){
    ct.ThrowIfCancellationRequested();string key=OnlineThumbnailLookup.CacheKey(row.Title);thumbnailAttempted.Add(key);
    try{using(var result=await OnlineThumbnailLookup.Find(row.Title,ct)){
     if(ct.IsCancellationRequested||IsDisposed||target!=current)return;foreach(var same in onlineResults.Where(x=>OnlineThumbnailLookup.CacheKey(x.Title)==key)){same.ThumbnailSource=result.Source;same.ThumbnailError=result.Error;}
     if(result.TemporarilyBlocked){thumbnailsPaused=true;break;}
     if(result.Image!=null&&!onlineThumbs.Images.ContainsKey(key)){onlineThumbs.Images.Add(key,result.Image);foreach(ListViewItem item in onlineFiles.Items){var tagged=item.Tag as OnlineResult;if(tagged!=null&&OnlineThumbnailLookup.CacheKey(tagged.Title)==key){item.ImageKey=key;item.ToolTipText=ThumbnailTip(tagged);}}onlineFiles.Invalidate();}
    }}catch(OperationCanceledException){thumbnailAttempted.Remove(key);throw;}catch(Exception ex){row.ThumbnailError=ex.Message;}
   }
  }catch(OperationCanceledException){}finally{if(!ct.IsCancellationRequested&&!IsDisposed&&target==current){thumbnailsLoading=false;if(onlineMode)RenderOnline();}}
 }
 string ThumbnailTip(OnlineResult r){string query=OnlineThumbnailLookup.SearchText(r.Title);string header="Metadata only • media transfer starts only after Stream locally or Download locally";if(r.ThumbnailSource.Length>0)return header+"\nThumbnail search: "+query+"\nImage source: "+r.ThumbnailSource;if(r.ThumbnailError.Length>0)return header+"\nThumbnail search: "+query+"\n"+r.ThumbnailError;return header+"\nThumbnail search: "+query;}
 void RenderOnline(){
  if(!onlineMode)return;string q=search.Text.Trim();string res=resolution.SelectedIndex<=0?"":resolution.SelectedItem.ToString();IEnumerable<OnlineResult> selected=onlineResults.Where(r=>r.Seeders>0&&(q.Length==0||r.Title.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||r.Source.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0));if(res.Length>0)selected=selected.Where(r=>r.Resolution.Equals(res,StringComparison.OrdinalIgnoreCase));
  if(sort.SelectedIndex==1)selected=selected.OrderBy(r=>r.Title,StringComparer.OrdinalIgnoreCase);else if(sort.SelectedIndex==2)selected=selected.OrderByDescending(r=>r.Size).ThenByDescending(r=>r.Seeders);else selected=selected.OrderByDescending(r=>r.Seeders).ThenByDescending(r=>r.Published);
  var rows=selected.ToList();onlineFiles.BeginUpdate();onlineFiles.Items.Clear();foreach(var r in rows){string published=r.Published==DateTime.MinValue?"—":r.Published.ToLocalTime().ToString("dd MMM yyyy HH:mm");var item=new ListViewItem(new[]{r.Title,r.Resolution,SizeText(r.Size),r.Seeders.ToString(),r.Leechers.ToString(),r.Source,published});string key=OnlineThumbnailLookup.CacheKey(r.Title);item.ImageKey=onlineThumbs.Images.ContainsKey(key)?key:"__placeholder";item.ToolTipText=ThumbnailTip(r);item.Tag=r;onlineFiles.Items.Add(item);}onlineFiles.EndUpdate();
  if(onlineSearching)status.Text="Retrieving torrent metadata for \""+onlineQueryFor+"\"… no media is being downloaded";else if(onlineError.Length>0)status.Text="Online search unavailable • "+onlineError;else status.Text=onlineFiles.Items.Count+" seeded online results"+(onlineFiles.Items.Count==0?" • No matching results with at least one seeder.":" • Select a result to Stream locally or Download locally");
  if(thumbnailsLoading)status.Text+=" • Loading artwork only…";else if(thumbnailsPaused)status.Text+=" • Thumbnail lookup paused by the search engine";else StartThumbnailLoading(rows);
 }
 static string SizeText(long n){if(n<=0)return "—";return n>=1073741824?(n/1073741824.0).ToString("0.0")+" GB":(n/1048576.0).ToString("0.0")+" MB";}
 void Play(){if(files.SelectedItems.Count>0)Launch(((Video)files.SelectedItems[0].Tag).Path);else status.Text="Select a video first, then choose Play.";}
 void StreamOnline(){if(onlineFiles.SelectedItems.Count==0){status.Text="Select an online result first.";return;}var r=(OnlineResult)onlineFiles.SelectedItems[0].Tag;if(TransferBridge.Stream(this,r))status.Text="VideoShelf streaming player opened • torrent transfer starts in the player";}
 void DownloadOnline(){if(onlineFiles.SelectedItems.Count==0){status.Text="Select an online result first.";return;}var r=(OnlineResult)onlineFiles.SelectedItems[0].Tag;string suggested=current!=null?current.Path:root;if(TransferBridge.Download(this,r,suggested))status.Text="VideoShelf download window opened • transfer starts there";}
 void CopyOnline(){if(onlineFiles.SelectedItems.Count==0){status.Text="Select an online result first.";return;}try{Clipboard.SetText(((OnlineResult)onlineFiles.SelectedItems[0].Tag).Link);status.Text="Torrent/magnet link copied to the clipboard. No media was downloaded.";}catch(Exception ex){status.Text="Could not copy link: "+ex.Message;}}
 void Launch(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,"Could not open this item. Check that it still exists and that a suitable application is installed.\n\n"+ex.Message,"VideoShelf",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
}
}
