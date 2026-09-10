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
 public static void CaptureReZero(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,"re:zero");Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.PullScreenshotPortrait("re:zero"))throw new Exception("A real thumbnail could not be pulled for re:zero; refusing to capture a placeholder Home screenshot.");shelf.ShowHome();Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureReZeroDetail(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-detail-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,"re:zero");Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.OpenScreenshotCollection("re:zero"))throw new Exception("VideoShelf did not open the real re:zero collection view correctly.");Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureOnlineResults(string displayName,string query,string sourceUrl,string outputPath,int expectedResults){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-online-screenshot-"+Guid.NewGuid().ToString("N"));try{Directory.CreateDirectory(root);FolderNaming.CreateCollection(root,displayName);Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareScreenshotLibrary(root);if(!shelf.PrepareOnlineScreenshot(displayName,query,sourceUrl,expectedResults))throw new Exception("VideoShelf did not receive enough seeded online results from the metadata source.");Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,5000);}finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
 public static void CaptureMockupSearch(string outputPath){Init();using(var shelf=new Shelf()){PrepareWindow(shelf);shelf.PrepareMockupSearch();Settle(shelf);SaveWindow(shelf,outputPath);}Verify(outputPath,12000);}
 static void Init(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);}
 static void PrepareWindow(Shelf shelf){
  var host=new Panel{Size=new Size(1536,1024),BackColor=Color.Black};host.CreateControl();
  shelf.MinimumSize=Size.Empty;shelf.MaximumSize=Size.Empty;shelf.TopLevel=false;shelf.StartPosition=FormStartPosition.Manual;shelf.Location=Point.Empty;shelf.Size=new Size(1536,1024);host.Controls.Add(shelf);shelf.Show();
  shelf.SetBounds(0,0,1536,1024,BoundsSpecified.All);shelf.ClientSize=new Size(1536,1024);shelf.PerformLayout();Application.DoEvents();
 }
 static void Settle(Shelf shelf){shelf.PerformLayout();foreach(Control c in shelf.Controls)c.PerformLayout();shelf.Refresh();Application.DoEvents();Thread.Sleep(350);Application.DoEvents();}
 static void SaveWindow(Shelf shelf,string outputPath){using(var bitmap=new Bitmap(1536,1024,PixelFormat.Format32bppArgb)){shelf.DrawToBitmap(bitmap,new Rectangle(0,0,1536,1024));string directory=Path.GetDirectoryName(outputPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);bitmap.Save(outputPath,ImageFormat.Png);}Control host=shelf.Parent;shelf.Hide();if(host!=null){host.Controls.Remove(shelf);host.Dispose();}}
 static void Verify(string outputPath,long minBytes){if(!File.Exists(outputPath)||new FileInfo(outputPath).Length<minBytes)throw new Exception("Screenshot was not created correctly.");}
}

sealed partial class Shelf {
 internal void PrepareScreenshotLibrary(string path){
  generation++;portraitScan.Cancel();onlineScan.Cancel();thumbnailScan.Cancel();current=null;root=path;fetchingPortraits=false;onlineSearching=false;thumbnailsLoading=false;thumbnailsPaused=false;skipped=0;selectedOnline=null;homePath.Text=root;settingsPath.Text=root;addFolder.Enabled=true;ClearCards();foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();people=new List<Person>();foreach(string dir in Directory.GetDirectories(path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))people.Add(new Person{Path=dir,Name=FolderNaming.DisplayName(dir)});RenderHome();
 }
 internal bool PullScreenshotPortrait(string name){Person person=people.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));if(person==null)return false;PortraitLookup.Forget(name);using(var result=PortraitLookup.Find(name,CancellationToken.None).GetAwaiter().GetResult()){if(result.Photo==null)return false;ApplyPortrait(person,result);}RenderHome();Application.DoEvents();return person.Photo!=null;}
 internal bool OpenScreenshotCollection(string displayName){Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));if(person==null)return false;OpenPerson(person);DateTime deadline=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<deadline){Application.DoEvents();if(current==person&&section==ShellSection.Collections&&collectionView.Visible&&statusLeft.Text.IndexOf("local video",StringComparison.OrdinalIgnoreCase)>=0)return true;Thread.Sleep(50);}return current==person&&collectionView.Visible;}
 internal bool PrepareOnlineScreenshot(string displayName,string query,string sourceUrl,int expectedResults){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));if(person==null)return false;var source=new OnlineSettings{Url=sourceUrl,ApiKey="",AutoSearch=false};List<OnlineResult> found;using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10)))found=TorznabSearch.Search(query,source,timeout.Token).GetAwaiter().GetResult();found=found.Where(r=>r.Seeders>0).Take(24).ToList();if(found.Count<expectedResults)return false;generation++;current=person;onlineSearching=false;onlineError="";onlineQueryFor=query;onlineSettings=source;onlineResults=found;selectedOnline=found.FirstOrDefault();thumbnailsLoading=false;thumbnailsPaused=true;onlineQuery.Text=query;RefreshSourceFilter();ShowSection(searchView,ShellSection.Search);RenderOnline();Application.DoEvents();return onlineCards.Controls.Count>=expectedResults;
 }
 internal void PrepareMockupSearch(){
  generation++;current=new Person{Name="re:zero",Path=Path.Combine(Path.GetTempPath(),"re-zero")};onlineQuery.Text="re:zero";onlineQueryFor="re:zero";onlineError="";onlineSearching=false;thumbnailsPaused=true;thumbnailsLoading=false;onlineResults=new List<OnlineResult>();
  string[] titles={"Re:Zero – Starting Life in Another World (S1) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (S2) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (S3) [1080p] x264 Dual Audio Subbed","Re:Zero – Memory Snow (OVA) [1080p] x264 Dual Audio Subbed","Re:Zero – The Frozen Bond (OVA) [1080p] x264 Dual Audio Subbed","Re:Zero – Starting Life in Another World (Director's Cut) [1080p] x264 Dual Audio Subbed","Re:Zero – Specials [1080p] x264 Subbed","Re:Zero – Starting Life in Another World (S1) [720p] x264 Dual Audio Subbed"};
  int[] seeds={1245,892,620,431,398,287,210,198};int[] leeches={32,18,14,6,4,3,1,5};double[] gb={8.4,9.1,6.8,1.2,1.3,7.6,2.1,4.3};DateTime[] dates={new DateTime(2020,10,14),new DateTime(2021,3,24),new DateTime(2024,4,3),new DateTime(2019,6,28),new DateTime(2019,11,8),new DateTime(2020,1,1),new DateTime(2020,5,10),new DateTime(2020,10,14)};
  for(int i=0;i<24;i++){int x=i<8?i:i%8;int seed=i<8?seeds[x]:Math.Max(1,150-(i-8)*6);string title=i<8?titles[x]:titles[x]+" archive "+(i-7);onlineResults.Add(new OnlineResult{Title=title,Resolution=titles[x].IndexOf("720p",StringComparison.OrdinalIgnoreCase)>=0?"720p":"1080p",Size=(long)(gb[x]*1073741824d),Seeders=seed,Leechers=leeches[x],Source="Nyaa.si",Published=dates[x],Link="magnet:?xt=urn:btih:"+new string((char)('A'+(i%6)),40)});}
  selectedOnline=onlineResults[0];RefreshSourceFilter();suppress=true;categoryFilter.SelectedItem="Anime";resolution.SelectedIndex=0;sourceFilter.SelectedIndex=0;suppress=false;ShowSection(searchView,ShellSection.Search);RenderOnline();int index=0;foreach(var card in onlineCards.Controls.OfType<OnlineResultCard>()){using(var image=MakeFixturePreview(index++))card.SetPreview(image);}RenderInspector();activityTimer.Stop();navDownloads.SetBadge(1);navStreaming.SetBadge(0);SetStatus("Ready","Results are metadata only. No files are downloaded.");
 }
 Image MakeFixturePreview(int index){var bmp=new Bitmap(292,156);using(Graphics g=Graphics.FromImage(bmp)){g.Clear(Color.FromArgb(22+(index%8)*2,42+(index%8)*3,62+(index%8)*2));using(var b=new SolidBrush(Color.FromArgb(40,XdolfTheme.AccentBlue)))g.FillEllipse(b,150-(index%8)*3,-30+(index%8)*4,190,190);using(var b=new SolidBrush(Color.FromArgb(65,255,255,255)))g.FillRectangle(b,0,105,292,51);using(var f=new Font("Segoe UI",22,FontStyle.Bold))TextRenderer.DrawText(g,index%2==0?"Re:ZERO":"Re:Zero",f,new Rectangle(8,42,276,50),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}return bmp;}
}
}
