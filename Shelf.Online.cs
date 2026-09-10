using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VideoShelf {
sealed partial class Shelf {
 void ConfigureOnline(){
  onlineSettings=OnlineSettings.Load();using(var d=new OnlineSourceDialog(onlineSettings))if(d.ShowDialog(this)==DialogResult.OK&&d.Value!=null){onlineSettings=d.Value;try{onlineSettings.Save();}catch(Exception ex){MessageBox.Show(this,"Could not save online source settings.\n\n"+ex.Message,"VideoShelf");return;}onlineResults.Clear();selectedOnline=null;onlineQueryFor="";onlineError="";if(section==ShellSection.Search)SearchOnline(onlineQuery.Text,true);if(section==ShellSection.Settings)ShowSettings();RefreshDashboardHome();}
 }
 async void SearchOnline(string query,bool showStatus){
  onlineSettings=OnlineSettings.Load();query=(query??"").Trim();if(query.Length==0){onlineError="Enter a search query.";if(showStatus){ShowSearch();RenderOnline();}return;}
  suppress=true;sourceFilter.SelectedIndex=0;categoryFilter.SelectedIndex=0;resolution.SelectedIndex=0;suppress=false;
  onlineScan.Cancel();onlineScan.Dispose();onlineScan=new System.Threading.CancellationTokenSource();thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();thumbnailAttempted.Clear();thumbnailsLoading=false;thumbnailsPaused=false;var ct=onlineScan.Token;Person target=current;onlineSearching=true;onlineError="";onlineQueryFor=query;if(showStatus){ShowSection(searchView,ShellSection.Search);RenderOnline();}
  try{
   List<OnlineResult> found=null;bool useBuiltIn=!onlineSettings.Configured;
   if(onlineSettings.Configured){
    try{found=await TorznabSearch.Search(query,onlineSettings,ct);useBuiltIn=found==null||found.Count==0;}
    catch(OperationCanceledException){throw;}
    catch{useBuiltIn=true;}
   }
   if(useBuiltIn)found=await BuiltInOnlineSearch.Search(query,ct);
   if(found==null)found=new List<OnlineResult>();
   if(ct.IsCancellationRequested||IsDisposed||target!=current)return;onlineResults=found.Where(r=>r.Seeders>0).ToList();selectedOnline=onlineResults.FirstOrDefault();RefreshSourceFilter();
  }
  catch(OperationCanceledException){return;}catch(Exception ex){if(ct.IsCancellationRequested||IsDisposed||target!=current)return;onlineResults.Clear();selectedOnline=null;onlineError=ex.Message;}
  finally{if(!ct.IsCancellationRequested&&!IsDisposed&&target==current){onlineSearching=false;if(section==ShellSection.Search)RenderOnline();else if(current!=null)RenderLocal();}}
 }
 void RefreshSourceFilter(){
  string old=sourceFilter.SelectedItem==null?"All sources":sourceFilter.SelectedItem.ToString();suppress=true;sourceFilter.Items.Clear();sourceFilter.Items.Add("All sources");foreach(string source in onlineResults.Where(r=>r.Seeders>0).Select(r=>DisplaySource(r.Source)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))sourceFilter.Items.Add(source);int index=sourceFilter.Items.IndexOf(old);sourceFilter.SelectedIndex=index>=0?index:0;suppress=false;
 }
 void RenderOnline(){
  if(searchView.IsDisposed)return;ApplyFinalPolish();LayoutSearchSurface();LayoutFinalPolish();string src=sourceFilter.SelectedIndex<=0?"":sourceFilter.SelectedItem.ToString();string cat=categoryFilter.SelectedIndex<=0?"":categoryFilter.SelectedItem.ToString();string res=resolution.SelectedIndex<=0?"":resolution.SelectedItem.ToString();IEnumerable<OnlineResult> selected=onlineResults.Where(r=>r.Seeders>0);
  if(src.Length>0)selected=selected.Where(r=>DisplaySource(r.Source).Equals(src,StringComparison.OrdinalIgnoreCase));if(cat.Length>0)selected=selected.Where(r=>MetadataLabels.Category(r).Equals(cat,StringComparison.OrdinalIgnoreCase));if(res.Length>0)selected=selected.Where(r=>r.Resolution.Equals(res,StringComparison.OrdinalIgnoreCase));var rows=selected.OrderByDescending(r=>r.Seeders).ThenByDescending(r=>r.Published).ToList();
  if(selectedOnline!=null&&!rows.Contains(selectedOnline))selectedOnline=rows.FirstOrDefault();if(selectedOnline==null&&rows.Count>0)selectedOnline=rows[0];
  onlineCards.SuspendLayout();while(onlineCards.Controls.Count>0)onlineCards.Controls[0].Dispose();foreach(var r in rows){var card=new OnlineResultCard(r);card.Selected=r==selectedOnline;card.ResultSelected+=delegate{SelectOnline(card.Result,card);};card.ResultActivated+=delegate{SelectOnline(card.Result,card);StreamOnline();};tips.SetToolTip(card,"Metadata only\n"+r.Title+"\n"+r.Seeders+" seeders • "+r.Leechers+" leechers");onlineCards.Controls.Add(card);}onlineCards.ResumeLayout();ResizeOnlineCards();RenderInspector();
  string q=onlineQueryFor.Length>0?onlineQueryFor:onlineQuery.Text.Trim();searchHeading.Text=q.Length==0?"Search":"Search results for “"+q+"”";
  if(onlineSearching){searchCount.Text=onlineSettings.Configured?"Searching your metadata source + built-in fallback…":"Searching built-in metadata sources…";SetStatus("Searching…","No media is being downloaded.");}
  else if(onlineError.Length>0){searchCount.Text="Search unavailable";SetStatus("Search unavailable",onlineError);}
  else if(rows.Count==0&&onlineResults.Count>0){searchCount.Text="No results match the current filters";SetStatus("Filtered results","Set source, category and resolution to All to show every seeded result.");}
  else if(rows.Count==0){searchCount.Text="No seeded results found";SetStatus("No seeded results","Try a broader search.");}
  else{searchCount.Text="Found "+rows.Count+" result"+(rows.Count==1?"":"s")+" (metadata only)";SetStatus("Ready","Results are metadata only. No files are downloaded.");}
  if(!onlineSearching&&!thumbnailsLoading&&!thumbnailsPaused&&rows.Count>0)StartThumbnailLoading(rows);
 }
 void SelectOnline(OnlineResult result,OnlineResultCard card){selectedOnline=result;foreach(Control c in onlineCards.Controls){var rc=c as OnlineResultCard;if(rc!=null){rc.Selected=rc==card;rc.Invalidate();}}RenderInspector();}
 void RenderInspector(){
  if(inspectorImage.Image!=null){var old=inspectorImage.Image;inspectorImage.Image=null;old.Dispose();}while(inspectorTags.Controls.Count>0)inspectorTags.Controls[0].Dispose();
  bool has=selectedOnline!=null;downloadOnline.Enabled=streamOnline.Enabled=copyLink.Enabled=viewFiles.Enabled=has;if(!has){inspectorTitle.Text="Select a result";inspectorMeta.Text="Choose a seeded result to see its metadata and available actions.";return;}
  var r=selectedOnline;inspectorTitle.Text=r.Title;foreach(string tag in MetadataLabels.Tags(r.Title,r.Resolution))inspectorTags.Controls.Add(MakePill(tag));
  string date=r.Published==DateTime.MinValue?"—":r.Published.ToLocalTime().ToString("yyyy-MM-dd");inspectorMeta.Text="Total size\t"+SizeText(r.Size)+"\r\n\r\nRelease date\t"+date+"\r\n\r\nSource\t\t"+DisplaySource(r.Source)+"\r\n\r\nSeeders\t\t"+r.Seeders.ToString("N0")+"\r\n\r\nLeechers\t"+r.Leechers.ToString("N0")+"\r\n\r\nTorrent metadata only. Use View files to inspect the torrent contents before starting a transfer.";
  var card=onlineCards.Controls.OfType<OnlineResultCard>().FirstOrDefault(c=>c.Result==r);if(card!=null&&card.Preview!=null)inspectorImage.Image=new Bitmap(card.Preview);
 }
 Label MakePill(string text){using(var f=new Font("Segoe UI",8f)){int width=Math.Min(100,TextRenderer.MeasureText(text,f).Width+16);var l=new Label{Text=text,Width=width,Height=25,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(224,234,244),BackColor=Color.FromArgb(18,39,57),Margin=new Padding(0,0,6,6),Font=new Font("Segoe UI",8f)};return l;}}
 async void StartThumbnailLoading(IEnumerable<OnlineResult> displayed){
  if(thumbnailsLoading||thumbnailsPaused||onlineSearching||section!=ShellSection.Search||IsDisposed)return;var pending=displayed.Where(r=>r.Seeders>0).GroupBy(r=>OnlineThumbnailLookup.CacheKey(r.Title),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).Where(r=>!thumbnailAttempted.Contains(OnlineThumbnailLookup.CacheKey(r.Title))).Take(40).ToArray();if(pending.Length==0)return;
  thumbnailScan.Cancel();thumbnailScan.Dispose();thumbnailScan=new System.Threading.CancellationTokenSource();var ct=thumbnailScan.Token;Person target=current;thumbnailsLoading=true;SetStatus("Loading artwork…","Only small preview images are fetched; no video data.");
  try{foreach(var row in pending){ct.ThrowIfCancellationRequested();string key=OnlineThumbnailLookup.CacheKey(row.Title);thumbnailAttempted.Add(key);try{using(var result=await OnlineThumbnailLookup.Find(row.Title,ct)){if(ct.IsCancellationRequested||IsDisposed||target!=current)return;foreach(var same in onlineResults.Where(x=>OnlineThumbnailLookup.CacheKey(x.Title)==key)){same.ThumbnailSource=result.Source;same.ThumbnailError=result.Error;}if(result.TemporarilyBlocked){thumbnailsPaused=true;break;}if(result.Image!=null){foreach(var card in onlineCards.Controls.OfType<OnlineResultCard>().Where(c=>OnlineThumbnailLookup.CacheKey(c.Result.Title)==key))card.SetPreview(result.Image);if(selectedOnline!=null&&OnlineThumbnailLookup.CacheKey(selectedOnline.Title)==key)RenderInspector();}}}catch(OperationCanceledException){thumbnailAttempted.Remove(key);throw;}catch(Exception ex){row.ThumbnailError=ex.Message;}}}
  catch(OperationCanceledException){}finally{if(!ct.IsCancellationRequested&&!IsDisposed&&target==current){thumbnailsLoading=false;if(section==ShellSection.Search)SetStatus("Ready",thumbnailsPaused?"Thumbnail lookup paused by the image source.":"Results are metadata only. No files are downloaded.");}}
 }
 static string DisplaySource(string s){if(string.IsNullOrWhiteSpace(s))return "Indexer";Uri u;if(Uri.TryCreate(s,UriKind.Absolute,out u))return u.Host;return s;}
 void StreamOnline(){if(selectedOnline==null){SetStatus("Select a result","Choose a seeded result first.");return;}if(TransferBridge.Stream(this,selectedOnline)){RefreshActivityBadges();RefreshDashboardHome();SetStatus("Streaming opened","Torrent data starts only after this explicit action.");}}
 void DownloadOnline(){if(selectedOnline==null){SetStatus("Select a result","Choose a seeded result first.");return;}string suggested=current!=null?current.Path:root;if(TransferBridge.Download(this,selectedOnline,suggested)){RefreshActivityBadges();RefreshDashboardHome();SetStatus("Download opened","Choose the destination in the transfer window.");}}
 void CopyOnline(){if(selectedOnline==null){SetStatus("Select a result","Choose a result first.");return;}try{Clipboard.SetText(selectedOnline.Link);SetStatus("Copied link","No media was downloaded.");}catch(Exception ex){SetStatus("Copy failed",ex.Message);}}
 void ViewOnlineFiles(){if(selectedOnline==null){SetStatus("Select a result","Choose a result first.");return;}if(TransferBridge.ViewFiles(this,selectedOnline))SetStatus("Torrent files","Retrieving torrent metadata only.");}
}
}
