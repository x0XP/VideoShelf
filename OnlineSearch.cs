using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

namespace VideoShelf {
sealed class OnlineResult {
 public string Title="", Link="", PageUrl="", Source="", Resolution="Other", ThumbnailSource="", ThumbnailError="";
 public long Size;
 public int Seeders, Leechers;
 public DateTime Published;
}

sealed class OnlineSettings {
 public string Url="", ApiKey="";
 public bool AutoSearch=true;
 public bool Configured { get { Uri uri; return Uri.TryCreate(Url,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https"); } }
 static readonly string file=AppDataPaths.MigrateFile("online-source.txt");
 public static OnlineSettings Load(){
  var s=new OnlineSettings();
  try{
   if(!File.Exists(file))return s;
   string[] lines=File.ReadAllLines(file);
   if(lines.Length>0)s.Url=lines[0].Trim();
   if(lines.Length>1&&lines[1].Length>0)try{s.ApiKey=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(lines[1]),null,DataProtectionScope.CurrentUser));}catch{s.ApiKey="";}
   if(lines.Length>2){bool auto;if(bool.TryParse(lines[2],out auto))s.AutoSearch=auto;}
  }catch{}
  return s;
 }
 public void Save(){
  Directory.CreateDirectory(Path.GetDirectoryName(file));
  string key=ApiKey.Length==0?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(ApiKey),null,DataProtectionScope.CurrentUser));
  File.WriteAllLines(file,new[]{Url.Trim(),key,AutoSearch.ToString()});
 }
}

static class TorznabSearch {
 static readonly HttpClient http=CreateClient();
 static HttpClient CreateClient(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var h=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var c=new HttpClient(h){Timeout=TimeSpan.FromSeconds(30)};
  c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.3");
  return c;
 }
 static string Elem(XElement item,string local){
  var e=item.Elements().FirstOrDefault(x=>x.Name.LocalName.Equals(local,StringComparison.OrdinalIgnoreCase));
  return e==null?"":e.Value.Trim();
 }
 static string Attr(XElement item,string name){
  foreach(var e in item.Descendants().Where(x=>x.Name.LocalName.Equals("attr",StringComparison.OrdinalIgnoreCase))){
   var n=e.Attribute("name");var v=e.Attribute("value");
   if(n!=null&&v!=null&&n.Value.Equals(name,StringComparison.OrdinalIgnoreCase))return v.Value.Trim();
  }
  return "";
 }
 static int IntValue(string value){int n;return int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)?Math.Max(0,n):0;}
 static long LongValue(string value){long n;return long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)?Math.Max(0,n):0;}
 static string Resolution(string title){
  Match m=Regex.Match(title,@"(?<!\d)(2160|1080|720|576|540|480|360)p\b",RegexOptions.IgnoreCase);
  if(m.Success)return m.Groups[1].Value+"p";
  if(Regex.IsMatch(title,@"\b(4k|uhd)\b",RegexOptions.IgnoreCase))return "2160p";
  if(Regex.IsMatch(title,@"\b(fhd|full[ ._-]?hd)\b",RegexOptions.IgnoreCase))return "1080p";
  return "Other";
 }
 static string SourceFor(XElement item,string page){
  string source=Attr(item,"indexer");
  if(source.Length==0)source=Elem(item,"jackettindexer");
  if(source.Length==0)source=Elem(item,"author");
  if(source.Length==0){Uri uri;if(Uri.TryCreate(page,UriKind.Absolute,out uri))source=uri.Host;}
  return source.Length==0?"Indexer":source;
 }
 static string QueryUrl(OnlineSettings settings,string query){
  string url=settings.Url.Trim();
  string sep=url.IndexOf('?')>=0?"&":"?";
  url+=sep+"t=search&q="+Uri.EscapeDataString(query.Trim());
  if(settings.ApiKey.Trim().Length>0)url+="&apikey="+Uri.EscapeDataString(settings.ApiKey.Trim());
  return url;
 }
 public static async Task<List<OnlineResult>> Search(string query,OnlineSettings settings,CancellationToken ct){
  if(!settings.Configured)throw new InvalidOperationException("Configure a Torznab source first.");
  if(query.Trim().Length==0)return new List<OnlineResult>();
  string url=QueryUrl(settings,query);
  string xml;
  using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseContentRead,ct).ConfigureAwait(false)){
   if(!response.IsSuccessStatusCode)throw new HttpRequestException("Online source returned HTTP "+(int)response.StatusCode+".");
   xml=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
  }
  ct.ThrowIfCancellationRequested();
  XDocument doc=XDocument.Parse(xml,LoadOptions.None);
  var error=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName.Equals("error",StringComparison.OrdinalIgnoreCase));
  if(error!=null){var d=error.Attribute("description");throw new InvalidOperationException(d==null?"The online source returned an error.":d.Value);}
  var results=new List<OnlineResult>();
  foreach(var item in doc.Descendants().Where(x=>x.Name.LocalName.Equals("item",StringComparison.OrdinalIgnoreCase))){
   ct.ThrowIfCancellationRequested();
   string title=Elem(item,"title");if(title.Length==0)continue;
   int seeders=IntValue(Attr(item,"seeders"));
   if(seeders<=0)continue; // Hard rule: never display unseeded results.
   int leechers=IntValue(Attr(item,"leechers"));if(leechers==0)leechers=IntValue(Attr(item,"peers"));
   string magnet=Attr(item,"magneturl");
   XElement enclosure=item.Elements().FirstOrDefault(x=>x.Name.LocalName.Equals("enclosure",StringComparison.OrdinalIgnoreCase));
   string enclosureUrl="",enclosureLength="";
   if(enclosure!=null){var u=enclosure.Attribute("url");var l=enclosure.Attribute("length");if(u!=null)enclosureUrl=u.Value;if(l!=null)enclosureLength=l.Value;}
   string page=Elem(item,"link");if(page.Length==0)page=Elem(item,"guid");
   string link=magnet.Length>0?magnet:(enclosureUrl.Length>0?enclosureUrl:page);
   if(link.Length==0)continue;
   long size=LongValue(Attr(item,"size"));if(size==0)size=LongValue(enclosureLength);
   DateTime published=DateTime.MinValue;DateTime.TryParse(Elem(item,"pubDate"),CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out published);
   results.Add(new OnlineResult{Title=title,Link=link,PageUrl=page,Source=SourceFor(item,page),Resolution=Resolution(title),Size=size,Seeders=seeders,Leechers=leechers,Published=published});
  }
  return results.OrderByDescending(x=>x.Seeders).ThenByDescending(x=>x.Published).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase).ToList();
 }
}

sealed class OnlineSourceDialog : Form {
 readonly TextBox url=new TextBox(), key=new TextBox(); readonly CheckBox autoSearch=new CheckBox();
 public OnlineSettings Value;
 public OnlineSourceDialog(OnlineSettings current){
  Text="Online search source";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;ClientSize=new Size(610,310);BackColor=Color.FromArgb(17,22,32);ForeColor=Color.White;Font=new Font("Segoe UI",10);
  var intro=new Label{Left=22,Top=18,Width=565,Height=52,ForeColor=Color.FromArgb(156,170,193),Text="Use a Torznab-compatible endpoint from software such as Jackett or Prowlarr. VideoShelf searches that endpoint and only displays results with at least one seeder."};
  var urlLabel=new Label{Left=22,Top=84,Width=160,Height=22,Text="Torznab API URL"};url.SetBounds(22,108,565,27);url.Text=current.Url;
  var keyLabel=new Label{Left=22,Top=149,Width=160,Height=22,Text="API key (if required)"};key.SetBounds(22,173,565,27);key.Text=current.ApiKey;key.UseSystemPasswordChar=true;
  autoSearch.SetBounds(22,214,360,25);autoSearch.Text="Search automatically when a person is opened";autoSearch.Checked=current.AutoSearch;autoSearch.ForeColor=Color.White;
  var save=new Button{Text="Save",Left=405,Top=257,Width=86,Height=32,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancel",Left=501,Top=257,Width=86,Height=32,DialogResult=DialogResult.Cancel};
  foreach(var b in new[]{save,cancel}){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Color.FromArgb(28,34,47);b.ForeColor=Color.White;}
  Controls.AddRange(new Control[]{intro,urlLabel,url,keyLabel,key,autoSearch,save,cancel});AcceptButton=save;CancelButton=cancel;
 }
 protected override void OnFormClosing(FormClosingEventArgs e){
  if(DialogResult==DialogResult.OK){
   string u=url.Text.Trim();Uri uri;
   if(u.Length>0&&(!Uri.TryCreate(u,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))){MessageBox.Show(this,"Enter a valid HTTP or HTTPS Torznab API URL, or leave it blank to disable online search.","VideoShelf");e.Cancel=true;return;}
   Value=new OnlineSettings{Url=u,ApiKey=key.Text.Trim(),AutoSearch=autoSearch.Checked};
  }
  base.OnFormClosing(e);
 }
}
}
