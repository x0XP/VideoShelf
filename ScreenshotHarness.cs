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

 public static void CaptureReZeroVideos(string outputPath){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-videos-screenshot-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);
   string collection=FolderNaming.CreateCollection(root,"re:zero");
   string season1=Path.Combine(collection,"Season 1");
   string season2=Path.Combine(collection,"Season 2");
   Directory.CreateDirectory(season1);
   Directory.CreateDirectory(season2);
   CreateTestVideo(season1,"Re Zero - S01E01 - The End of the Beginning and the Beginning of the End.mkv",1572864);
   CreateTestVideo(season1,"Re Zero - S01E02 - Reunion with the Witch.mp4",1310720);
   CreateTestVideo(season1,"Re Zero - S01E03 - Starting Life from Zero in Another World.mkv",1835008);
   CreateTestVideo(season1,"Re Zero - S01E04 - The Happy Roswaal Mansion Family.mkv",2097152);
   CreateTestVideo(season2,"Re Zero - S02E01 - Each One's Promise.mkv",1703936);
   CreateTestVideo(season2,"Re Zero - S02E02 - The Next Location.mkv",1966080);

   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   using(var shelf=new Shelf()){
    PrepareWindow(shelf);
    shelf.PrepareScreenshotLibrary(root);
    if(!shelf.OpenScreenshotCollection("re:zero",6))throw new Exception("VideoShelf did not pull the re:zero test videos through its normal local scanner.");
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
 static void CreateTestVideo(string directory,string name,int bytes){
  string path=Path.Combine(directory,name);
  using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
   byte[] buffer=new byte[8192];
   int remaining=bytes;
   while(remaining>0){int count=Math.Min(buffer.Length,remaining);stream.Write(buffer,0,count);remaining-=count;}
  }
 }
}

sealed partial class Shelf {
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

 internal bool OpenScreenshotCollection(string name,int expectedVideos){
  Person person=people.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
  if(person==null)return false;
  OpenPerson(person);
  DateTime until=DateTime.UtcNow.AddSeconds(8);
  while(DateTime.UtcNow<until){
   Application.DoEvents();
   if(current==person&&files.Items.Count>=expectedVideos)return true;
   Thread.Sleep(50);
  }
  return current==person&&files.Items.Count>=expectedVideos;
 }
}
}
