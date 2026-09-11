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
 static HttpClient CreateClient(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var h=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};var c=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15)};c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.4");return c;}
 static string Key(string name){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()))).Replace("-","");}
 static string FileFor(string name){return Path.Combine(cache,Key(name)+".jpg");}
 static string PrimaryQuery(string name){return (name??"").Trim();}
 public static string SearchUrl(string name){return SearchUrlFor(PrimaryQuery(name));}
 static string SearchUrlFor(string query){return "https://duckduckgo.com/?q="+Uri.EscapeDataString(query)+"&iax=images&ia=images&kp=1";}
 public static void Forget(string name){string f=FileFor(name);if(File.Exists(f))File.Delete(f);if(File.Exists(f+".source"))File.Delete(f+".source");}
 static Image Decode(byte[] bytes){using(var ms=new MemoryStream(bytes))using(var im=Image.FromStream(ms,true,true)){if((long)im.Width*im.Height>16000000||im.Width<40||im.Height<40)throw new InvalidDataException("Unsuitable image dimensions.");double scale=Math.Min(1,400.0/Math.Max(im.Width,im.Height));return new Bitmap(im,Math.Max(1,(int)(im.Width*scale)),Math.Max(1,(int)(im.Height*scale)));}}
 public static void Store(string name,Image photo,string source){Directory.CreateDirectory(cache);string f=FileFor(name),temp=f+"."+Guid.NewGuid().ToString("N")+".tmp";try{photo.Save(temp,ImageFormat.Jpeg);if(File.Exists(f))File.Delete(f);File.Move(temp,f);File.WriteAllText(f+".source",source);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static PortraitResult Local(string name,string path){byte[] data=File.ReadAllBytes(path);if(data.Length>8*1024*1024)throw new InvalidDataException("Choose an image under 8 MB.");var result=new PortraitResult{Photo=Decode(data),Source="Chosen image"};try{Store(name,result.Photo,result.Source);result.FromCache=true;return result;}catch{result.Dispose();throw;}}
 static async Task<byte[]> Get(string url,int limit,CancellationToken ct){
  Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new InvalidDataException("Only HTTPS image sources are supported.");
  using(var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
   if((int)response.StatusCode==202||(int)response.StatusCode==403||(int)response.StatusCode==429)throw new LookupBlockedException("Automatic lookup paused for 10 minutes after an access check or rate limit. Use Search images from the card menu.");
   if(response.StatusCode!=HttpStatusCode.OK)throw new HttpRequestException("Search unavailable or blocked (HTTP "+(int)response.StatusCode+").");
   if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Response too large.");
   using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream())using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct)){
    deadline.CancelAfter(TimeSpan.FromSeconds(15));byte[] buffer=new byte[8192];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0){if(output.Length+count>limit)throw new InvalidDataException("Response too large.");output.Write(buffer,0,count);}return output.ToArray();
   }
  }
 }
 static string Value(Dictionary<string,object> row,string key){object v;return row.TryGetValue(key,out v)&&v!=null?v.ToString():"";}
 static string[] MatchWords(string name){return Regex.Matches(name??"",@"[A-Za-z0-9]+",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Value).Where(x=>x.Length>1).ToArray();}
 static async Task<PortraitResult> SearchOne(string name,string query,CancellationToken ct){
  string html=Encoding.UTF8.GetString(await Get(SearchUrlFor(query),2*1024*1024,ct).ConfigureAwait(false));
  if(html.IndexOf("anomaly.js",StringComparison.OrdinalIgnoreCase)>=0||html.IndexOf("challenge-form",StringComparison.OrdinalIgnoreCase)>=0)throw new LookupBlockedException("DuckDuckGo needs a browser check. Use Search images from the card menu.");
  Match token=Regex.Match(html,"vqd=['\"](?<token>[0-9-]+)['\"]");
  if(!token.Success)throw new InvalidDataException("DuckDuckGo did not provide image search data.");
  string endpoint="https://duckduckgo.com/i.js?l=uk-en&o=json&q="+Uri.EscapeDataString(query)+"&vqd="+Uri.EscapeDataString(token.Groups["token"].Value)+"&p=1";
  string json=Encoding.UTF8.GetString(await Get(endpoint,2*1024*1024,ct).ConfigureAwait(false));
  var payload=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<Dictionary<string,object>>(json);
  object rows;if(payload==null||!payload.TryGetValue("results",out rows))return null;
  var items=rows as System.Collections.IEnumerable;if(items==null)return null;
  string[] words=MatchWords(name);int tried=0;
  foreach(object item in items){ct.ThrowIfCancellationRequested();var row=item as Dictionary<string,object>;if(row==null)continue;
   string title=Value(row,"title");if(words.Length>0&&!words.All(w=>title.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0))continue;
   if(++tried>8)break;string image=Value(row,"thumbnail");if(image.Length==0)image=Value(row,"image");if(image.Length==0)continue;
   try{var photo=Decode(await Get(image,5*1024*1024,ct).ConfigureAwait(false));return new PortraitResult{Photo=photo,Source=Value(row,"url")};}
   catch(OperationCanceledException){throw;}catch(LookupBlockedException){throw;}catch{}
  }
  return null;
 }
 public static async Task<PortraitResult> Find(string name,CancellationToken ct){
  await gate.WaitAsync(ct).ConfigureAwait(false);
  try{
   ct.ThrowIfCancellationRequested();string f=FileFor(name);
   if(File.Exists(f))try{return new PortraitResult{Photo=Decode(File.ReadAllBytes(f)),FromCache=true,Source=File.Exists(f+".source")?File.ReadAllText(f+".source"):"Cached image"};}catch{}
   if(DateTime.UtcNow<blockedUntil)return new PortraitResult{Error="Automatic lookup paused after an access check. Use Search images from the card menu."};
   await Task.Delay(1200,ct).ConfigureAwait(false);
   string query=PrimaryQuery(name);
   PortraitResult result=await SearchOne(name,query,ct).ConfigureAwait(false);
   if(result==null)result=await SearchOne(name,query+" artwork",ct).ConfigureAwait(false);
   if(result==null)result=await SearchOne(name,query+" image",ct).ConfigureAwait(false);
   if(result!=null)return result;
   return new PortraitResult{Error="No usable matching image found. Right-click to choose an image."};
  }catch(LookupBlockedException ex){blockedUntil=DateTime.UtcNow.AddMinutes(10);return new PortraitResult{Error=ex.Message};}catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;return new PortraitResult{Error="Image lookup timed out. Right-click to retry."};}catch(Exception ex){return new PortraitResult{Error=ex.Message};}finally{gate.Release();}
 }
}
}
