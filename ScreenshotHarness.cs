using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;

namespace VideoShelf {
static class ScreenshotHarness {
 public static void CaptureHomeLayout(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-home-layout-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,"re:zero");Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);shelf.ShowHome();Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureReZero(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,"re:zero");Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(55))){if(!shelf.PullScreenshotPortrait("re:zero",timeout.Token))throw new Exception("A real thumbnail could not be pulled for re:zero; refusing to capture a placeholder artwork proof.");}shelf.ShowCollectionsBrowser();Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureReZeroDetail(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-detail-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,"re:zero");Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.OpenScreenshotCollection("re:zero"))throw new Exception("VideoShelf did not open the real re:zero collection view correctly.");Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureOnlineResults(string displayName,string query,string sourceUrl,string outputPath,int expectedResults){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-online-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,displayName);Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.PrepareOnlineScreenshot(displayName,query,sourceUrl,expectedResults))throw new Exception("VideoShelf did not receive enough seeded online results from the metadata source.");Settle(shelf);SaveWindow(shelf,outputPath,1366,860);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureBuiltInOnlineResults(string displayName,string query,string outputPath,int expectedResults){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-built-in-online-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,displayName);Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.PrepareBuiltInOnlineScreenshot(displayName,query,expectedResults))throw new Exception("VideoShelf built-in Find online did not return enough seeded results.");shelf.WaitForFirstOnlineThumbnailForCapture(6500);Settle(shelf);SaveWindow(shelf,outputPath,1366,860);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureMockupSearch(string outputPath){Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareMockupSearch();Settle(shelf);SaveWindow(shelf,outputPath,1366,860);}Verify(outputPath,12000);}
 static void Init(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);}
 static void PrepareWindow(Shelf shelf){
  var host=new Panel{Size=new Size(1100,820),BackColor=Color.Black};host.CreateControl();
  shelf.MinimumSize=Size.Empty;shelf.MaximumSize=Size.Empty;shelf.TopLevel=false;shelf.StartPosition=FormStartPosition.Manual;shelf.Location=Point.Empty;shelf.Size=new Size(1044,788);host.Controls.Add(shelf);shelf.Show();shelf.PerformLayout();Application.DoEvents();
 }
 static void Settle(Shelf shelf){shelf.PerformLayout();foreach(Control c in shelf.Controls)c.PerformLayout();shelf.Refresh();Application.DoEvents();Thread.Sleep(350);Application.DoEvents();}
 static void SaveWindow(Shelf shelf,string outputPath){SaveWindow(shelf,outputPath,1536,1024);}
 static void SaveWindow(Shelf shelf,string outputPath,int width,int height){
  const int titleHeight=34,statusHeight=30,sidebarWidth=244;
  int bodyHeight=Math.Max(320,height-titleHeight-statusHeight),mainWidth=Math.Max(500,width-sidebarWidth-2);
  Control title=shelf.Controls.Cast<Control>().FirstOrDefault(c=>c is Panel&&c.Dock==DockStyle.Top&&c.Height>=30&&c.Height<=36);
  Control status=shelf.Controls.Cast<Control>().FirstOrDefault(c=>c is Panel&&c.Dock==DockStyle.Bottom);
  Control view=shelf.ActiveViewForCapture();
  Control side=shelf.SidebarForCapture();
  if(title==null||status==null||view==null||side==null)throw new InvalidOperationException("VideoShelf capture surfaces were not available.");

  title.Dock=DockStyle.None;title.SetBounds(0,0,width,titleHeight);title.Anchor=AnchorStyles.Top|AnchorStyles.Left;title.PerformLayout();
  status.Dock=DockStyle.None;status.SetBounds(0,0,width,statusHeight);status.Anchor=AnchorStyles.Top|AnchorStyles.Left;status.PerformLayout();
  side.Dock=DockStyle.None;side.SetBounds(0,0,sidebarWidth,bodyHeight);side.Anchor=AnchorStyles.Top|AnchorStyles.Left;side.PerformLayout();
  view.Dock=DockStyle.None;view.SetBounds(0,0,mainWidth,bodyHeight);view.Anchor=AnchorStyles.Top|AnchorStyles.Left;
  if(shelf.IsSearchViewForCapture(view))shelf.LayoutSearchForCapture(mainWidth,bodyHeight);else view.PerformLayout();
  Application.DoEvents();

  using(var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
   using(Graphics g=Graphics.FromImage(bitmap)){g.Clear(XdolfTheme.Background);}
   title.DrawToBitmap(bitmap,new Rectangle(0,0,width,titleHeight));
   side.DrawToBitmap(bitmap,new Rectangle(1,titleHeight,sidebarWidth,bodyHeight));
   view.DrawToBitmap(bitmap,new Rectangle(245,titleHeight,mainWidth,bodyHeight));
   status.DrawToBitmap(bitmap,new Rectangle(0,titleHeight+bodyHeight,width,statusHeight));
   using(Graphics g=Graphics.FromImage(bitmap)){using(var p=new Pen(XdolfTheme.Outline))g.DrawRectangle(p,0,0,width-1,height-1);}
   string directory=Path.GetDirectoryName(outputPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);bitmap.Save(outputPath,ImageFormat.Png);
  }
  Control host=shelf.Parent;shelf.Hide();if(host!=null){host.Controls.Remove(shelf);host.Dispose();}
 }
 static void Verify(string outputPath,long minBytes){if(!File.Exists(outputPath)||new FileInfo(outputPath).Length<minBytes)throw new Exception("Screenshot was not created correctly.");}
}

sealed partial class Shelf {
 internal void PrepareScreenshotLibrary(string path){
  generation++;portraitScan.Cancel();onlineScan.Cancel();thumbnailScan.Cancel();current=null;root=path;fetchingPortraits=false;onlineSearching=false;thumbnailsLoading=false;thumbnailsPaused=false;skipped=0;selectedOnline=null;homePath.Text=root;settingsPath.Text=root;addFolder.Enabled=true;ClearCards();foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();people=new List<Person>();foreach(string dir in Directory.GetDirectories(path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))people.Add(new Person{Path=dir,Name=FolderNaming.DisplayName(dir)});RenderHome();RefreshDashboardHome();
 }
 internal bool PullScreenshotPortrait(string name,CancellationToken ct){Person person=people.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));if(person==null)return false;PortraitLookup.Forget(name);using(var result=PortraitLookup.Find(name,ct).GetAwaiter().GetResult()){if(result.Photo==null)return false;ApplyPortrait(person,result);}RenderHome();Application.DoEvents();return person.Photo!=null;}
 internal bool OpenScreenshotCollection(string displayName){Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));if(person==null)return false;OpenPerson(person);DateTime deadline=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<deadline){Application.DoEvents();if(current==person&&section==ShellSection.Collections&&collectionView.Visible&&statusLeft.Text.IndexOf("local video",StringComparison.OrdinalIgnoreCase)>=0)return true;Thread.Sleep(50);}return current==person&&collectionView.Visible;}
 internal bool PrepareOnlineScreenshot(string displayName,string query,string sourceUrl,int expectedResults){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));if(person==null)return false;var source=new OnlineSettings{Url=sourceUrl,ApiKey="",AutoSearch=false};List<OnlineResult> found;using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10)))found=TorznabSearch.Search(query,source,timeout.Token).GetAwaiter().GetResult();found=found.Where(r=>r.Seeders>0).Take(24).ToList();if(found.Count<expectedResults)return false;PrepareOnlineFixtureState(person,query,found,source,false);return onlineCards.Controls.Count>=expectedResults;
 }
 internal bool PrepareBuiltInOnlineScreenshot(string displayName,string query,int expectedResults){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));if(person==null)return false;List<OnlineResult> found;using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(18)))found=BuiltInOnlineSearch.Search(query,timeout.Token).GetAwaiter().GetResult();found=found.Where(r=>r.Seeders>0&&!string.IsNullOrWhiteSpace(r.Link)).Take(24).ToList();if(found.Count<expectedResults)return false;PrepareOnlineFixtureState(person,query,found,new OnlineSettings{AutoSearch=true},true);return onlineCards.Controls.Count>=expectedResults;
 }
 void PrepareOnlineFixtureState(Person person,string query,List<OnlineResult> found,OnlineSettings source,bool allowThumbnails){
  generation++;current=person;onlineSearching=false;onlineError="";onlineQueryFor=query;onlineSettings=source;onlineResults=found;selectedOnline=found.FirstOrDefault();thumbnailsLoading=false;thumbnailsPaused=!allowThumbnails;onlineQuery.Text=query;suppress=true;categoryFilter.SelectedIndex=0;resolution.SelectedIndex=0;suppress=false;RefreshSourceFilter();ShowSection(searchView,ShellSection.Search);RenderOnline();Application.DoEvents();
 }
 internal void WaitForFirstOnlineThumbnailForCapture(int milliseconds){
  DateTime deadline=DateTime.UtcNow.AddMilliseconds(Math.Max(0,milliseconds));
  while(DateTime.UtcNow<deadline&&!IsDisposed){
   Application.DoEvents();
   var selectedCard=onlineCards.Controls.OfType<OnlineResultCard>().FirstOrDefault(c=>c.Result==selectedOnline);
   if(selectedCard!=null&&selectedCard.Preview!=null){RenderInspector();Application.DoEvents();return;}
   if(thumbnailsPaused&&!thumbnailsLoading)return;
   Thread.Sleep(75);
  }
 }
 internal void PrepareMockupSearch(){
  generation++;current=new Person{Name="re:zero",Path=Path.Combine(Path.GetTempPath(),"re-zero")};onlineQuery.Text="re:zero";onlineQueryFor="re:zero";onlineError="";onlineSearching=false;thumbnailsPaused=true;thumbnailsLoading=false;onlineResults=new List<OnlineResult>();
  string[] titles={"Re:Zero – Starting Life in Another World (S1) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (S2) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (S3) [1080p] x264 Dual Audio Subbed","Re:Zero – Memory Snow (OVA) [1080p] x264 Dual Audio Subbed","Re:Zero – The Frozen Bond (OVA) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (Director's Cut) [1080p] x264 Dual Audio Subbed","Re:Zero – Specials [1080p] x264 Subbed","Re:Zero – Starting Life in Another World (S1) [720p] x264 Dual Audio Subbed"};
  int[] seeds={1245,892,620,431,398,287,210,198};int[] leeches={32,18,14,6,4,3,1,5};double[] gb={8.4,9.1,6.8,1.2,1.3,7.6,2.1,4.3};DateTime[] dates={new DateTime(2020,10,14),new DateTime(2021,3,24),new DateTime(2024,4,3),new DateTime(2019,6,28),new DateTime(2019,11,8),new DateTime(2020,1,1),new DateTime(2020,5,10),new DateTime(2020,10,14)};
  for(int i=0;i<24;i++){int x=i<8?i:i%8;int seed=i<8?seeds[x]:Math.Max(1,150-(i-8)*6);string title=i<8?titles[x]:titles[x]+" archive "+(i-7);onlineResults.Add(new OnlineResult{Title=title,Resolution=titles[x].IndexOf("720p",StringComparison.OrdinalIgnoreCase)>=0?"720p":"1080p",Size=(long)(gb[x]*1073741824d),Seeders=seed,Leechers=leeches[x],Source="Nyaa.si",Published=dates[x],Link="magnet:?xt=urn:btih:"+new string((char)('A'+(i%6)),40)});}
  selectedOnline=onlineResults[0];RefreshSourceFilter();suppress=true;categoryFilter.SelectedItem="Anime";resolution.SelectedIndex=0;sourceFilter.SelectedIndex=0;suppress=false;ShowSection(searchView,ShellSection.Search);RenderOnline();int index=0;foreach(var card in onlineCards.Controls.OfType<OnlineResultCard>()){using(var image=MakeFixturePreview(index++))card.SetPreview(image);}RenderInspector();activityTimer.Stop();navDownloads.SetBadge(1);navStreaming.SetBadge(0);SetStatus("Ready","Results are metadata only. No files are downloaded.");ValidateMockupState();
 }
 void ValidateMockupState(){
  if(onlineResults.Count!=24||onlineResults.Any(r=>r.Seeders<=0))throw new InvalidOperationException("Mockup search state failed the seeded-result invariant.");
  if(onlineCards.Controls.OfType<OnlineResultCard>().Count()!=24||selectedOnline==null)throw new InvalidOperationException("Mockup search cards were not rendered correctly.");
  if(sourceVisual==null||categoryVisual==null||resolutionVisual==null||languageVisual==null||sourceFilter.Visible||categoryFilter.Visible||resolution.Visible||languageFilter.Visible)throw new InvalidOperationException("Dark search filters are not active.");
  if(sourceVisual.Height!=SearchFilterHeight||categoryVisual.Height!=SearchFilterHeight||resolutionVisual.Height!=SearchFilterHeight||languageVisual.Height!=SearchFilterHeight||sourceVisual.Top!=categoryVisual.Top||sourceVisual.Top!=resolutionVisual.Top||sourceVisual.Top!=languageVisual.Top)throw new InvalidOperationException("Search filter controls are not aligned to the shared geometry.");
  if(languageFilter.SelectedItem==null||!languageFilter.SelectedItem.ToString().Equals("English",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Language filter must default to English.");
  if(!languageFilter.Items.Contains("All languages")||!languageFilter.Items.Contains("Japanese")||!languageFilter.Items.Contains("Spanish")||!languageFilter.Items.Contains("Multi-language")||!languageFilter.Items.Contains("Unspecified"))throw new InvalidOperationException("Language filter options are incomplete.");
  if(!MetadataLabels.MatchesLanguage("Example release [English]","English")||MetadataLabels.MatchesLanguage("Example release [Japanese]","English")||!MetadataLabels.MatchesLanguage("Example release [Japanese]","Japanese")||!MetadataLabels.MatchesLanguage("Example release [Dual Audio]","English")||!MetadataLabels.MatchesLanguage("Example release","English"))throw new InvalidOperationException("Language filter classification regression detected.");
  if(inspectorMetadataVisual==null||inspectorMeta.Visible||downloadVisual==null||streamVisual==null||!downloadVisual.Enabled||!streamVisual.Enabled)throw new InvalidOperationException("Result inspector actions are not active.");
  if(inspectorTags.Height<56)throw new InvalidOperationException("Inspector tags do not have enough height to display wrapped metadata tags.");
 }
 Image MakeFixturePreview(int index){var bmp=new Bitmap(292,156);using(Graphics g=Graphics.FromImage(bmp)){g.Clear(Color.FromArgb(22+(index%8)*2,42+(index%8)*3,62+(index%8)*2));using(var b=new SolidBrush(Color.FromArgb(40,XdolfTheme.AccentBlue)))g.FillEllipse(b,150-(index%8)*3,-30+(index%8)*4,190,190);using(var b=new SolidBrush(Color.FromArgb(65,255,255,255)))g.FillRectangle(b,0,105,292,51);using(var f=new Font("Segoe UI",22,FontStyle.Bold))TextRenderer.DrawText(g,index%2==0?"Re:ZERO":"Re:Zero",f,new Rectangle(8,42,276,50),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}return bmp;}
}
}
