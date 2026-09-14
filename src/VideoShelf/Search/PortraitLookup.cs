using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace VideoShelf {
sealed class PortraitResult : IDisposable {
 public Image Photo; public bool FromCache; public string Source=""; public string Error="";
 public void Dispose(){if(Photo!=null){Photo.Dispose();Photo=null;}}
}
sealed class LookupBlockedException : Exception { public LookupBlockedException(string message):base(message){} }
static class PortraitLookup {
 static DateTime blockedUntil=DateTime.MinValue;
 static readonly HttpClient http=CreateClient();
 static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
 static readonly string cache=AppDataPaths.MigrateDirectory("Portraits");
 static HttpClient CreateClient(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var h=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var c=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15)};
  c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
  c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
  return c;
 }
 static string Key(string name){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()))).Replace("-","");}
 static string FileFor(string name){return Path.Combine(cache,Key(name)+".jpg");}
 static string PrimaryQuery(string name){return (name??"").Trim();}
 public static string SearchUrl(string name){return SearchUrlFor(PrimaryQuery(name));}
 static string SearchUrlFor(string query){return "https://duckduckgo.com/?q="+Uri.EscapeDataString(query)+"&iax=images&ia=images&kp=-2";}
 public static void Forget(string name){string f=FileFor(name);if(File.Exists(f))File.Delete(f);if(File.Exists(f+".source"))File.Delete(f+".source");}
 static Image Decode(byte[] bytes){using(var ms=new MemoryStream(bytes))using(var im=Image.FromStream(ms,true,true)){if((long)im.Width*im.Height>16000000||im.Width<40||im.Height<40)throw new InvalidDataException("Unsuitable image dimensions.");double scale=Math.Min(1,400.0/Math.Max(im.Width,im.Height));return new Bitmap(im,Math.Max(1,(int)(im.Width*scale)),Math.Max(1,(int)(im.Height*scale)));}}
 public static void Store(string name,Image photo,string source){Directory.CreateDirectory(cache);string f=FileFor(name),temp=f+"."+Guid.NewGuid().ToString("N")+".tmp";try{photo.Save(temp,ImageFormat.Jpeg);if(File.Exists(f))File.Delete(f);File.Move(temp,f);File.WriteAllText(f+".source",source??"");}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static PortraitResult Local(string name,string path){byte[] data=File.ReadAllBytes(path);if(data.Length>8*1024*1024)throw new InvalidDataException("Choose an image under 8 MB.");var result=new PortraitResult{Photo=Decode(data),Source="Chosen image"};try{Store(name,result.Photo,result.Source);result.FromCache=true;return result;}catch{result.Dispose();throw;}}
 static async Task<byte[]> Get(string url,int limit,CancellationToken ct){
  Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new InvalidDataException("Only HTTPS image sources are supported.");
  using(var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
   if((int)response.StatusCode==202||(int)response.StatusCode==403||(int)response.StatusCode==429)throw new LookupBlockedException("Image source returned an access check or rate limit.");
   if(response.StatusCode!=HttpStatusCode.OK)throw new HttpRequestException("Search unavailable or blocked (HTTP "+(int)response.StatusCode+").");
   if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Response too large.");
   using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream())using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct)){
    deadline.CancelAfter(TimeSpan.FromSeconds(15));byte[] buffer=new byte[8192];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0){if(output.Length+count>limit)throw new InvalidDataException("Response too large.");output.Write(buffer,0,count);}return output.ToArray();
   }
  }
 }
 static string Value(Dictionary<string,object> row,string key){object v;return row.TryGetValue(key,out v)&&v!=null?v.ToString():"";}
 static string[] MatchWords(string name){return Regex.Matches(name??"",@"[A-Za-z0-9]+",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Value).Where(x=>x.Length>1).ToArray();}
 static async Task<List<Dictionary<string,object>>> SearchDuckDuckGoRows(string query,CancellationToken ct){
  string html=Encoding.UTF8.GetString(await Get(SearchUrlFor(query),2*1024*1024,ct).ConfigureAwait(false));
  if(html.IndexOf("anomaly.js",StringComparison.OrdinalIgnoreCase)>=0||html.IndexOf("challenge-form",StringComparison.OrdinalIgnoreCase)>=0)throw new LookupBlockedException("DuckDuckGo needs a browser check.");
  Match token=Regex.Match(html,"vqd=['\"](?<token>[0-9-]+)['\"]");
  if(!token.Success)throw new InvalidDataException("DuckDuckGo did not provide image search data.");
  string endpoint="https://duckduckgo.com/i.js?l=uk-en&o=json&q="+Uri.EscapeDataString(query)+"&vqd="+Uri.EscapeDataString(token.Groups["token"].Value)+"&p=-2&kp=-2";
  string json=Encoding.UTF8.GetString(await Get(endpoint,2*1024*1024,ct).ConfigureAwait(false));
  var payload=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<Dictionary<string,object>>(json);
  object rows;if(payload==null||!payload.TryGetValue("results",out rows))return new List<Dictionary<string,object>>();
  var items=rows as System.Collections.IEnumerable;if(items==null)return new List<Dictionary<string,object>>();
  return items.Cast<object>().Select(x=>x as Dictionary<string,object>).Where(x=>x!=null).ToList();
 }
 static async Task<List<Dictionary<string,object>>> SearchBingRows(string query,CancellationToken ct){
  string url="https://www.bing.com/images/search?q="+Uri.EscapeDataString(query)+"&form=HDRSC3&first=1&adlt=off";
  string html=Encoding.UTF8.GetString(await Get(url,3*1024*1024,ct).ConfigureAwait(false));
  var rows=new List<Dictionary<string,object>>();
  foreach(Match match in Regex.Matches(html,@"\bm=""(?<meta>\{&quot;.*?\})""",RegexOptions.IgnoreCase|RegexOptions.Singleline)){
   Dictionary<string,object> meta=null;
   try{meta=new JavaScriptSerializer{MaxJsonLength=256*1024}.Deserialize<Dictionary<string,object>>(WebUtility.HtmlDecode(match.Groups["meta"].Value));}catch{}
   if(meta==null)continue;
   string image=Value(meta,"murl"),thumbnail=Value(meta,"turl");if(image.Length==0&&thumbnail.Length==0)continue;
   rows.Add(new Dictionary<string,object>{{"image",image},{"thumbnail",thumbnail},{"url",Value(meta,"purl")},{"title",Value(meta,"t")+" "+Value(meta,"desc")}});
   if(rows.Count>=50)break;
  }
  return rows;
 }
 static async Task<List<Dictionary<string,object>>> SearchRows(string query,CancellationToken ct){
  List<Dictionary<string,object>> primary=null;
  if(DateTime.UtcNow>=blockedUntil){
   try{primary=await SearchDuckDuckGoRows(query,ct).ConfigureAwait(false);if(primary.Count>0)return primary;}
   catch(OperationCanceledException){throw;}
   catch(LookupBlockedException){blockedUntil=DateTime.UtcNow.AddMinutes(10);}
   catch(HttpRequestException){}
   catch(InvalidDataException){}
  }
  try{var fallback=await SearchBingRows(query,ct).ConfigureAwait(false);if(fallback.Count>0)return fallback;}
  catch(OperationCanceledException){throw;}catch{}
  return primary??new List<Dictionary<string,object>>();
 }
 static bool MatchesName(Dictionary<string,object> row,string name){
  string combined=Value(row,"title")+" "+Value(row,"url")+" "+Value(row,"image")+" "+Value(row,"thumbnail");
  string[] words=MatchWords(name);if(words.Length==0)return true;
  int matched=words.Count(w=>combined.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0);
  return matched>=Math.Min(words.Length,words.Length<=2?1:2);
 }
 static async Task<PortraitResult> SearchOne(string name,string query,CancellationToken ct){
  List<Dictionary<string,object>> rows=await SearchRows(query,ct).ConfigureAwait(false);int tried=0;
  foreach(var row in rows){ct.ThrowIfCancellationRequested();if(row==null||!MatchesName(row,name))continue;if(++tried>10)break;
   string[] images=new[]{Value(row,"image"),Value(row,"thumbnail")}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   foreach(string image in images){
    try{var photo=Decode(await Get(image,5*1024*1024,ct).ConfigureAwait(false));string source=Value(row,"url");if(source.Length==0)source=image;return new PortraitResult{Photo=photo,Source=source};}
    catch(OperationCanceledException){throw;}catch(LookupBlockedException){continue;}catch{}
   }
  }
  return null;
 }
 public static async Task<PortraitResult> Find(string name,CancellationToken ct){
  await gate.WaitAsync(ct).ConfigureAwait(false);
  try{
   ct.ThrowIfCancellationRequested();string f=FileFor(name);
   if(File.Exists(f))try{return new PortraitResult{Photo=Decode(File.ReadAllBytes(f)),FromCache=true,Source=File.Exists(f+".source")?File.ReadAllText(f+".source"):"Cached image"};}catch{}
   await Task.Delay(350,ct).ConfigureAwait(false);
   string query=PrimaryQuery(name);
   PortraitResult result=await SearchOne(name,query,ct).ConfigureAwait(false);
   if(result==null)result=await SearchOne(name,query+" artwork",ct).ConfigureAwait(false);
   if(result==null)result=await SearchOne(name,query+" image",ct).ConfigureAwait(false);
   if(result!=null){try{Store(name,result.Photo,result.Source);}catch{}return result;}
   return new PortraitResult{Error="No usable matching image found. Right-click to choose an image."};
  }catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;return new PortraitResult{Error="Image lookup timed out. Right-click to retry."};}
   catch(Exception ex){return new PortraitResult{Error=ex.Message};}
   finally{gate.Release();}
 }
}
}
