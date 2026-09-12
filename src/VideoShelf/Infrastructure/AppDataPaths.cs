using System;
using System.IO;

namespace VideoShelf {
static class AppDataPaths {
 static readonly string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
 public static readonly string Root=Path.Combine(local,"VideoShelf");
 static readonly string LegacyRoot=Path.Combine(local,"PeopleShelf");

 public static string FilePath(string name){return Path.Combine(Root,name);}
 public static string DirectoryPath(string name){return Path.Combine(Root,name);}

 public static string MigrateFile(string name){
  string current=FilePath(name), legacy=Path.Combine(LegacyRoot,name);
  try{
   if(!File.Exists(current)&&File.Exists(legacy)){
    Directory.CreateDirectory(Root);
    File.Copy(legacy,current,false);
   }
  }catch{}
  return current;
 }

 public static string MigrateDirectory(string name){
  string current=DirectoryPath(name), legacy=Path.Combine(LegacyRoot,name);
  try{
   if(!Directory.Exists(current)&&Directory.Exists(legacy))CopyDirectory(legacy,current);
  }catch{}
  return current;
 }

 static void CopyDirectory(string source,string destination){
  Directory.CreateDirectory(destination);
  foreach(string file in Directory.GetFiles(source)){
   string target=Path.Combine(destination,Path.GetFileName(file));
   if(!File.Exists(target))File.Copy(file,target,false);
  }
  foreach(string dir in Directory.GetDirectories(source))CopyDirectory(dir,Path.Combine(destination,Path.GetFileName(dir)));
 }
}
}
