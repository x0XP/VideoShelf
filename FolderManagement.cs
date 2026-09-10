using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

namespace VideoShelf {
static class FolderNaming {
 const string MetadataFile=".videoshelf-name";
 static readonly char[] Invalid={'<','>',':','"','/','\\','|','?','*'};

 public static string DisplayName(string folderPath){
  try{
   string metadata=Path.Combine(folderPath,MetadataFile);
   if(File.Exists(metadata)){
    string value=File.ReadAllText(metadata,Encoding.UTF8).Trim();
    if(value.Length>0)return value;
   }
  }catch{}
  return Path.GetFileName(folderPath);
 }

 public static string SafeDirectoryName(string displayName){
  string value=(displayName??"").Trim();
  if(value.Length==0)throw new ArgumentException("Enter a folder name.");
  var b=new StringBuilder(value.Length);
  foreach(char c in value){
   bool invalid=c<32||Array.IndexOf(Invalid,c)>=0;
   b.Append(invalid?'-':c);
  }
  value=Regex.Replace(b.ToString(),@"\s*-+\s*","-");
  value=Regex.Replace(value,@"\s+"," ").Trim(' ','.','-');
  if(value.Length==0)value="Untitled";
  if(value.Length>100)value=value.Substring(0,100).Trim(' ','.','-');
  if(Regex.IsMatch(value,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$",RegexOptions.IgnoreCase))value+="_";
  return value;
 }

 public static string CreateCollection(string root,string displayName){
  if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))throw new DirectoryNotFoundException("Choose a library folder first.");
  displayName=(displayName??"").Trim();
  if(displayName.Length==0)throw new ArgumentException("Enter a folder name.");
  string safe=SafeDirectoryName(displayName), candidate=safe;
  int suffix=2;
  while(Directory.Exists(Path.Combine(root,candidate)))candidate=safe+" ("+(suffix++)+")";
  string path=Path.Combine(root,candidate);
  Directory.CreateDirectory(path);
  try{File.WriteAllText(Path.Combine(path,MetadataFile),displayName,Encoding.UTF8);}catch{try{Directory.Delete(path,true);}catch{}throw;}
  return path;
 }

 public static void SelfTest(){
  string root=Path.Combine(Path.GetTempPath(),"VideoShelf-folder-test-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);
   string path=CreateCollection(root,"re:zero");
   if(!Directory.Exists(path))throw new Exception("Test folder was not created.");
   if(Path.GetFileName(path).IndexOf(':')>=0)throw new Exception("Windows-invalid colon was not sanitized.");
   if(DisplayName(path)!="re:zero")throw new Exception("Display name was not preserved as re:zero.");
  }finally{try{if(Directory.Exists(root))Directory.Delete(root,true);}catch{}}
 }
}

sealed class AddFolderDialog : Form {
 readonly TextBox nameBox=new TextBox();
 public string FolderName { get { return nameBox.Text.Trim(); } }
 public AddFolderDialog(){
  Text="Add folder";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;ClientSize=new Size(430,160);BackColor=Color.FromArgb(17,22,32);ForeColor=Color.White;Font=new Font("Segoe UI",10);
  var label=new Label{Left=22,Top=20,Width=380,Height=24,Text="Folder / collection name"};
  nameBox.SetBounds(22,49,386,28);nameBox.BackColor=Color.FromArgb(28,34,47);nameBox.ForeColor=Color.White;nameBox.BorderStyle=BorderStyle.FixedSingle;
  var hint=new Label{Left=22,Top=82,Width=386,Height=23,ForeColor=Color.FromArgb(156,170,193),Text="Names such as re:zero are preserved in VideoShelf."};
  var add=new Button{Text="Add",Left=232,Top=115,Width=82,Height=31,DialogResult=DialogResult.OK};
  var cancel=new Button{Text="Cancel",Left=326,Top=115,Width=82,Height=31,DialogResult=DialogResult.Cancel};
  foreach(var b in new[]{add,cancel}){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Color.FromArgb(28,34,47);b.ForeColor=Color.White;}
  Controls.AddRange(new Control[]{label,nameBox,hint,add,cancel});AcceptButton=add;CancelButton=cancel;
  Shown+=delegate{nameBox.Focus();};
 }
 protected override void OnFormClosing(FormClosingEventArgs e){
  if(DialogResult==DialogResult.OK&&FolderName.Length==0){MessageBox.Show(this,"Enter a folder name.","VideoShelf");e.Cancel=true;return;}
  base.OnFormClosing(e);
 }
}
}
