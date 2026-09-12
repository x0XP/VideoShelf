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
using System.Web.Script.Serialization;

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
 public bool Configured {
  get {
   Uri uri;
   string candidate=(Url??"").Replace("{query}","test");
   return Uri.TryCreate(candidate,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https");
  }
 }
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
  c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
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
 static long SizeValue(string value){
  if(string.IsNullOrWhiteSpace(value))return 0;
  long n;if(long.TryParse(value.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out n))return Math.Max(0,n);
  Match m=Regex.Match(value.Trim(),@"^([0-9]+(?:\.[0-9]+)?)\s*([KMGT]?I?B)$",RegexOptions.IgnoreCase);
  if(!m.Success)return 0;
  double amount;if(!double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out amount))return 0;
  string unit=m.Groups[2].Value.ToUpperInvariant();double multiplier=1;
  if(unit=="KB"||unit=="KIB")multiplier=1024d;
  else if(unit=="MB"||unit=="MIB")multiplier=1024d*1024d;
  else if(unit=="GB"||unit=="GIB")multiplier=1024d*1024d*1024d;
  else if(unit=="TB"||unit=="TIB")multiplier=1024d*1024d*1024d*1024d;
  double bytes=amount*multiplier;
  return bytes<=0?0:bytes>=long.MaxValue?long.MaxValue:(long)bytes;
 }
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
  if(url.IndexOf("{query}",StringComparison.OrdinalIgnoreCase)>=0)
   return Regex.Replace(url,@"\{query\}",m=>Uri.EscapeDataString(query.Trim()),RegexOptions.IgnoreCase);
  string sep=url.IndexOf('?')>=0?"&":"?";
  url+=sep+"t=search&q="+Uri.EscapeDataString(query.Trim());
  if(settings.ApiKey.Trim().Length>0)url+="&apikey="+Uri.EscapeDataString(settings.ApiKey.Trim());
  return url;
 }
 static string MagnetFor(string explicitMagnet,string infoHash){
  if(!string.IsNullOrWhiteSpace(explicitMagnet)&&explicitMagnet.StartsWith("magnet:",StringComparison.OrdinalIgnoreCase))return explicitMagnet.Trim();
  string hash=(infoHash??"").Trim();
  return Regex.IsMatch(hash,@"^[A-Fa-f0-9]{40}$")?"magnet:?xt=urn:btih:"+hash:"";
 }
 static object JsonValue(Dictionary<string,object> item,string key){object value;return item!=null&&item.TryGetValue(key,out value)?value:null;}
 static string JsonString(Dictionary<string,object> item,string key){object value=JsonValue(item,key);return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture).Trim();}
 static int JsonInt(Dictionary<string,object> item,string key){object value=JsonValue(item,key);if(value==null)return 0;try{return Math.Max(0,Convert.ToInt32(value,CultureInfo.InvariantCulture));}catch{return IntValue(Convert.ToString(value,CultureInfo.InvariantCulture));}}
 static long JsonLong(Dictionary<string,object> item,string key){object value=JsonValue(item,key);if(value==null)return 0;try{return Math.Max(0,Convert.ToInt64(value,CultureInfo.InvariantCulture));}catch{return SizeValue(Convert.ToString(value,CultureInfo.InvariantCulture));}}
 static List<OnlineResult> ParseJson(string json,string requestUrl,CancellationToken ct){
  var serializer=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
  var root=serializer.DeserializeObject(json) as Dictionary<string,object>;
  if(root==null)return new List<OnlineResult>();
  object rawResults;if(!root.TryGetValue("results",out rawResults))return new List<OnlineResult>();
  var array=rawResults as object[];if(array==null)return new List<OnlineResult>();
  Uri requestUri;string source=Uri.TryCreate(requestUrl,UriKind.Absolute,out requestUri)?requestUri.Host:"JSON feed";
  var results=new List<OnlineResult>();
  foreach(object raw in array){
   ct.ThrowIfCancellationRequested();
   var item=raw as Dictionary<string,object>;if(item==null)continue;
   string title=JsonString(item,"title");if(title.Length==0)continue;
   int seeders=JsonInt(item,"seeders");if(seeders<=0)continue;
   int leechers=JsonInt(item,"leechers");
   string explicitMagnet=JsonString(item,"magnetUri");if(explicitMagnet.Length==0)explicitMagnet=JsonString(item,"magnet");
   string hash=JsonString(item,"infohash");if(hash.Length==0)hash=JsonString(item,"infoHash");
   string link=MagnetFor(explicitMagnet,hash);if(link.Length==0)continue;
   long size=JsonLong(item,"size");
   DateTime published=DateTime.MinValue;string date=JsonString(item,"createdAt");if(date.Length==0)date=JsonString(item,"published");
   if(date.Length>0)DateTime.TryParse(date,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces|DateTimeStyles.AssumeUniversal,out published);
   string page=JsonString(item,"url");if(page.Length==0)page=JsonString(item,"pageUrl");
   string itemSource=JsonString(item,"source");if(itemSource.Length==0)itemSource=source;
   results.Add(new OnlineResult{Title=title,Link=link,PageUrl=page,Source=itemSource,Resolution=Resolution(title),Size=size,Seeders=seeders,Leechers=leechers,Published=published});
  }
  return results.OrderByDescending(x=>x.Seeders).ThenByDescending(x=>x.Published).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase).ToList();
 }
 static List<OnlineResult> ParseXml(string xml,CancellationToken ct){
  XDocument doc=XDocument.Parse(xml,LoadOptions.None);
  var error=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName.Equals("error",StringComparison.OrdinalIgnoreCase));
  if(error!=null){var d=error.Attribute("description");throw new InvalidOperationException(d==null?"The online source returned an error.":d.Value);}
  var results=new List<OnlineResult>();
  foreach(var item in doc.Descendants().Where(x=>x.Name.LocalName.Equals("item",StringComparison.OrdinalIgnoreCase))){
   ct.ThrowIfCancellationRequested();
   string title=Elem(item,"title");if(title.Length==0)continue;
   int seeders=IntValue(Attr(item,"seeders"));if(seeders==0)seeders=IntValue(Elem(item,"seeders"));
   if(seeders<=0)continue;
   int leechers=IntValue(Attr(item,"leechers"));if(leechers==0)leechers=IntValue(Attr(item,"peers"));if(leechers==0)leechers=IntValue(Elem(item,"leechers"));if(leechers==0)leechers=IntValue(Elem(item,"peers"));
   string directMagnet=Attr(item,"magneturl");if(directMagnet.Length==0)directMagnet=Elem(item,"magnet");
   string infoHash=Attr(item,"infohash");if(infoHash.Length==0)infoHash=Elem(item,"infoHash");
   string magnet=MagnetFor(directMagnet,infoHash);
   XElement enclosure=item.Elements().FirstOrDefault(x=>x.Name.LocalName.Equals("enclosure",StringComparison.OrdinalIgnoreCase));
   string enclosureUrl="",enclosureLength="";
   if(enclosure!=null){var u=enclosure.Attribute("url");var l=enclosure.Attribute("length");if(u!=null)enclosureUrl=u.Value;if(l!=null)enclosureLength=l.Value;}
   string rawLink=Elem(item,"link"),guid=Elem(item,"guid"),page=rawLink;
   Uri guidUri;
   if((page.EndsWith(".torrent",StringComparison.OrdinalIgnoreCase)||page.IndexOf("/download/",StringComparison.OrdinalIgnoreCase)>=0)&&Uri.TryCreate(guid,UriKind.Absolute,out guidUri))page=guid;
   if(page.Length==0)page=guid;
   string link=magnet.Length>0?magnet:(enclosureUrl.Length>0?enclosureUrl:rawLink.Length>0?rawLink:page);
   if(link.Length==0)continue;
   long size=SizeValue(Attr(item,"size"));if(size==0)size=SizeValue(Elem(item,"size"));if(size==0)size=SizeValue(enclosureLength);
   DateTime published=DateTime.MinValue;DateTime.TryParse(Elem(item,"pubDate"),CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out published);
   results.Add(new OnlineResult{Title=title,Link=link,PageUrl=page,Source=SourceFor(item,page),Resolution=Resolution(title),Size=size,Seeders=seeders,Leechers=leechers,Published=published});
  }
  return results.OrderByDescending(x=>x.Seeders).ThenByDescending(x=>x.Published).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase).ToList();
 }
 public static async Task<List<OnlineResult>> Search(string query,OnlineSettings settings,CancellationToken ct){
  if(!settings.Configured)throw new InvalidOperationException("Configure an online metadata source first.");
  if(query.Trim().Length==0)return new List<OnlineResult>();
  string url=QueryUrl(settings,query);
  string payload;
  using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseContentRead,ct).ConfigureAwait(false)){
   if(!response.IsSuccessStatusCode)throw new HttpRequestException("Online source returned HTTP "+(int)response.StatusCode+".");
   payload=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
  }
  ct.ThrowIfCancellationRequested();
  string trimmed=payload.TrimStart();
  return trimmed.StartsWith("{")?ParseJson(payload,url,ct):ParseXml(payload,ct);
 }
}

sealed class OnlineSourceDialog : Form {
 readonly TextBox url=new TextBox(), key=new TextBox(); readonly CheckBox autoSearch=new CheckBox();
 public OnlineSettings Value;
 public OnlineSourceDialog(OnlineSettings current){
  Text="Online search source";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;ClientSize=new Size(610,326);BackColor=XdolfTheme.Background;ForeColor=XdolfTheme.Text;Font=new Font("Segoe UI",10);
  var intro=new Label{Left=22,Top=18,Width=565,Height=66,ForeColor=XdolfTheme.Muted,Text="Use a Torznab endpoint, XML/RSS search-feed URL, or compatible JSON metadata feed containing {query}. VideoShelf fetches metadata only and never shows results with zero seeders."};
  var urlLabel=new Label{Left=22,Top=92,Width=300,Height=22,Text="Torznab, XML/RSS or JSON search URL"};url.SetBounds(22,116,565,27);url.Text=current.Url;XdolfTheme.StyleInput(url);url.BorderStyle=BorderStyle.FixedSingle;
  var keyLabel=new Label{Left=22,Top=157,Width=160,Height=22,Text="API key (if required)"};key.SetBounds(22,181,565,27);key.Text=current.ApiKey;key.UseSystemPasswordChar=true;XdolfTheme.StyleInput(key);key.BorderStyle=BorderStyle.FixedSingle;
  autoSearch.SetBounds(22,222,390,25);autoSearch.Text="Search automatically when a collection is opened";autoSearch.Checked=current.AutoSearch;autoSearch.ForeColor=XdolfTheme.Text;
  var save=new Button{Text="Save",Left=405,Top=273,Width=86,Height=32,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancel",Left=501,Top=273,Width=86,Height=32,DialogResult=DialogResult.Cancel};
  foreach(var b in new[]{save,cancel})XdolfTheme.StyleButton(b);
  Controls.AddRange(new Control[]{intro,urlLabel,url,keyLabel,key,autoSearch,save,cancel});AcceptButton=save;CancelButton=cancel;
 }
 protected override void OnFormClosing(FormClosingEventArgs e){
  if(DialogResult==DialogResult.OK){
   string u=url.Text.Trim(),candidate=u.Replace("{query}","test");Uri uri;
   if(u.Length>0&&(!Uri.TryCreate(candidate,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))){MessageBox.Show(this,"Enter a valid HTTP or HTTPS metadata URL/search-feed template, or leave it blank to disable online search.","VideoShelf");e.Cancel=true;return;}
   Value=new OnlineSettings{Url=u,ApiKey=key.Text.Trim(),AutoSearch=autoSearch.Checked};
  }
  base.OnFormClosing(e);
 }
}
}
