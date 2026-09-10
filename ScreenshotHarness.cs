using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Collections.Generic;

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
    shelf.Size=new Size(1120,780);
    shelf.PrepareScreenshotLibrary(root);
    shelf.CreateControl();
    shelf.PerformLayout();
    foreach(Control c in shelf.Controls)c.PerformLayout();
    Application.DoEvents();
    using(var bitmap=new Bitmap(shelf.ClientSize.Width,shelf.ClientSize.Height,PixelFormat.Format32bppArgb)){
     shelf.DrawToBitmap(bitmap,new Rectangle(Point.Empty,shelf.ClientSize));
     string directory=Path.GetDirectoryName(outputPath);
     if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
     bitmap.Save(outputPath,ImageFormat.Png);
    }
   }
   if(!File.Exists(outputPath)||new FileInfo(outputPath).Length<1000)throw new Exception("Screenshot was not created correctly.");
  }finally{
   try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}
  }
 }
}

sealed partial class Shelf {
 internal void PrepareScreenshotLibrary(string path){
  generation++;
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
}
}
