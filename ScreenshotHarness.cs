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
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-screenshot-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);
   FolderNaming.CreateCollection(root,"re:zero");
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   using(var shelf=new Shelf()){
    PrepareWindow(shelf);
    shelf.PrepareScreenshotLibrary(root);
    if(!shelf.PullScreenshotPortrait("re:zero"))throw new Exception("A real thumbnail could not be pulled for re:zero; refusing to capture a placeholder screenshot.");
    Settle(shelf);
    SaveWindow(shelf,outputPath);
   }
   Verify(outputPath,5000);
  }finally{
   try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}
  }
 }

 public static void CaptureOnlineResults(string displayName,string query,string torznabUrl,string outputPath,int expectedResults){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-online-screenshot-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);
   FolderNaming.CreateCollection(root,displayName);
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   using(var shelf=new Shelf()){
    PrepareWindow(shelf);
    shelf.PrepareScreenshotLibrary(root);
    if(!shelf.PrepareOnlineScreenshot(displayName,query,torznabUrl,expectedResults))
     throw new Exception("VideoShelf did not receive enough seeded online results from the Torznab source.");
    DateTime until=DateTime.UtcNow.AddSeconds(6);
    while(DateTime.UtcNow<until&&shelf.ScreenshotThumbnailsLoading){Application.DoEvents();Thread.Sleep(50);}
    Settle(shelf);
    SaveWindow(shelf,outputPath);
   }
   Verify(outputPath,5000);
  }finally{
   try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}
  }
 }

 static void PrepareWindow(Shelf shelf){
  shelf.Size=new Size(1120,780);
  shelf.StartPosition=FormStartPosition.Manual;
  shelf.Location=new Point(20,20);
  shelf.Show();
  Application.DoEvents();
 }
 static void Settle(Shelf shelf){
  shelf.PerformLayout();
  foreach(Control c in shelf.Controls)c.PerformLayout();
  shelf.Refresh();
  Application.DoEvents();
  Thread.Sleep(300);
  Application.DoEvents();
 }
 static void SaveWindow(Shelf shelf,string outputPath){
  using(var bitmap=new Bitmap(shelf.ClientSize.Width,shelf.ClientSize.Height,PixelFormat.Format32bppArgb)){
   shelf.DrawToBitmap(bitmap,new Rectangle(Point.Empty,shelf.ClientSize));
   string directory=Path.GetDirectoryName(outputPath);
   if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
   bitmap.Save(outputPath,ImageFormat.Png);
  }
  shelf.Hide();
 }
 static void Verify(string outputPath,long minBytes){
  if(!File.Exists(outputPath)||new FileInfo(outputPath).Length<minBytes)throw new Exception("Screenshot was not created correctly.");
 }
}

sealed partial class Shelf {
 internal bool ScreenshotThumbnailsLoading { get { return thumbnailsLoading; } }

 internal void PrepareScreenshotLibrary(string path){
  generation++;
  portraitScan.Cancel();
  onlineScan.Cancel();
  thumbnailScan.Cancel();
  current=null;
  root=path;
  onlineMode=false;
  fetchingPortraits=false;
  onlineSearching=false;
  skipped=0;
  ClearSearch();
  SetSort(false);
  detail.Visible=false;
  cards.Visible=true;
  back.Visible=false;
  ClearCards();
  foreach(var p in people)if(p.Photo!=null)p.Photo.Dispose();
  people=new List<Person>();
  foreach(string dir in Directory.GetDirectories(path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)){
   people.Add(new Person{Path=dir,Name=FolderNaming.DisplayName(dir)});
  }
  title.Text="";
  subtitle.Text=root;
  SetHeader(false);
  Render();
 }

 internal bool PullScreenshotPortrait(string name){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
  if(person==null)return false;
  PortraitLookup.Forget(name);
  using(var result=PortraitLookup.Find(name,CancellationToken.None).GetAwaiter().GetResult()){
   if(result.Photo==null)return false;
   ApplyPortrait(person,result);
  }
  Render();
  Application.DoEvents();
  return person.Photo!=null;
 }

 internal bool PrepareOnlineScreenshot(string displayName,string query,string torznabUrl,int expectedResults){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(displayName,StringComparison.OrdinalIgnoreCase));
  if(person==null)return false;
  var source=new OnlineSettings{Url=torznabUrl,ApiKey="",AutoSearch=false};
  List<OnlineResult> found;
  using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10))){
   found=TorznabSearch.Search(query,source,timeout.Token).GetAwaiter().GetResult();
  }
  found=found.Where(r=>r.Seeders>0).Take(8).ToList();
  if(found.Count<expectedResults)return false;

  generation++;
  current=person;
  onlineMode=true;
  onlineSearching=false;
  onlineError="";
  onlineQueryFor=query;
  onlineSettings=source;
  onlineResults=found;
  ClearSearch();
  SetSort(true);
  back.Visible=true;
  cards.Visible=false;
  detail.Visible=true;
  localDetail.Visible=false;
  onlineDetail.Visible=true;
  onlineDetail.BringToFront();
  title.Text=displayName;
  SetHeader(true);
  subtitle.Text="Online results for "+displayName;
  onlineQuery.Text=query;
  RenderOnline();
  Application.DoEvents();
  return onlineFiles.Items.Count>=expectedResults;
 }
}
}
