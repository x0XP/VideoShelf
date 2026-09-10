using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace VideoShelf {
sealed class OnlineThumbnailResult : IDisposable {
 public Image Image; public bool FromCache, TemporarilyBlocked; public string Source=""; public string Error="";
 public void Dispose(){if(Image!=null){Image.Dispose();Image=null;}}
}

static class OnlineThumbnailLookup {
 static DateTime blockedUntil=DateTime.MinValue;
 static readonly HttpClient http=CreateClient();
 static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
 static readonly string cache=AppDataPaths.MigrateDirectory("OnlineThumbnails");

 static HttpClient CreateClient(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var h=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var c=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15)};
  c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.3");
  return c;
 }

 public static string SearchText(string title){
  string s=(title??"").Trim();
  s=Regex.Replace(s,@"\.(torrent|mkv|mp4|avi|mov|wmv|webm|m4v)$"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"(?<![A-Za-z0-9])(2160p|1080p|720p|576p|540p|480p|360p|4k|uhd|fhd|hdr10\+?|hdr|dolby[ ._-]?vision|dv|x264|x265|h\.?264|h\.?265|hevc|av1|10bit|8bit|bluray|blu[ ._-]?ray|bdrip|brrip|web[ ._-]?dl|webrip|webcap|hdtv|dvdrip|remux|aac(?:2\.0|5\.1)?|ac3|eac3|ddp(?:2\.0|5\.1)?|dts(?:hd)?|truehd|atmos|flac|mp3|proper|repack|rerip|internal|limited|extended|uncut|multi|dual[ ._-]?audio)(?![A-Za-z0-9])"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"(?<![A-Za-z0-9])(?:\d{1,2}bit|\d{3,4}kbps|\d{2,3}fps)(?![A-Za-z0-9])"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"[._]+"," ");
  s=Regex.Replace(s,@"[\[\]\(\)\{\}]"," ");
  s=Regex.Replace(s,@"\s+-\s*[A-Za-z0-9][A-Za-z0-9._-]{1,20}\s*$"," ");
  s=Regex.Replace(s,@"\s+"," ").Trim(' ','-','_','.');
  if(s.Length>140)s=s.Substring(0,140).Trim();
  return s.Length==0?(title??"").Trim():s;
 }

 public static string CacheKey(string title){
  string key=SearchText(title).ToLowerInvariant();
  using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","");
 }

 static string FileFor(string title){return Path.Combine(cache,CacheKey(title)+".jpg");}
 public static string SearchUrl(string title){return "https://duckduckgo.com/?q="+Uri.EscapeDataString(SearchText(title))+"&iax=images&ia=images&kp=1";}

 static async Task<byte[]> Get(string url,int limit,CancellationToken ct){
  Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new InvalidDataException("Only HTTPS image sources are supported.");
  using(var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
   if((int)response.StatusCode==202||(int)response.StatusCode==403||(int)response.StatusCode==429)throw new LookupBlockedException("Thumbnail lookup paused after a search-engine access check or rate limit.");
   if(response.StatusCode!=HttpStatusCode.OK)throw new HttpRequestException("Thumbnail search unavailable or blocked (HTTP "+(int)response.StatusCode+").");
   if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Thumbnail response too large.");
   using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream())using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct)){
    deadline.CancelAfter(TimeSpan.FromSeconds(15));byte[] buffer=new byte[8192];int count;
    while((count=await input.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0){if(output.Length+count>limit)throw new InvalidDataException("Thumbnail response too large.");output.Write(buffer,0,count);}return output.ToArray();
   }
  }
 }

 static Image DecodeAndFrame(byte[] bytes){
  using(var ms=new MemoryStream(bytes))using(var source=Image.FromStream(ms,true,true)){
   if((long)source.Width*source.Height>16000000||source.Width<40||source.Height<40)throw new InvalidDataException("Unsuitable thumbnail dimensions.");
   const int targetW=192,targetH=108;
   double scale=Math.Max((double)targetW/source.Width,(double)targetH/source.Height);
   double srcW=targetW/scale,srcH=targetH/scale;
   double srcX=(source.Width-srcW)/2.0,srcY=(source.Height-srcH)/2.0;
   var framed=new Bitmap(targetW,targetH,PixelFormat.Format32bppArgb);
   using(Graphics g=Graphics.FromImage(framed)){
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.CompositingQuality=CompositingQuality.HighQuality;
    g.DrawImage(source,new Rectangle(0,0,targetW,targetH),new RectangleF((float)srcX,(float)srcY,(float)srcW,(float)srcH),GraphicsUnit.Pixel);
   }
   return framed;
  }
 }

 static string Value(Dictionary<string,object> row,string key){object v;return row.TryGetValue(key,out v)&&v!=null?v.ToString():"";}

 static void Store(string title,Image image,string source){
  Directory.CreateDirectory(cache);string f=FileFor(title),temp=f+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{image.Save(temp,ImageFormat.Jpeg);if(File.Exists(f))File.Delete(f);File.Move(temp,f);File.WriteAllText(f+".source",source??"");}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }

 public static async Task<OnlineThumbnailResult> Find(string title,CancellationToken ct){
  await gate.WaitAsync(ct).ConfigureAwait(false);
  try{
   ct.ThrowIfCancellationRequested();string f=FileFor(title);
   if(File.Exists(f))try{return new OnlineThumbnailResult{Image=DecodeAndFrame(File.ReadAllBytes(f)),FromCache=true,Source=File.Exists(f+".source")?File.ReadAllText(f+".source"):"Cached search result"};}catch{}
   if(DateTime.UtcNow<blockedUntil)return new OnlineThumbnailResult{Error="Thumbnail lookup is temporarily paused after a search-engine access check.",TemporarilyBlocked=true};
   await Task.Delay(850,ct).ConfigureAwait(false);
   string query=SearchText(title);
   string html=Encoding.UTF8.GetString(await Get(SearchUrl(title),2*1024*1024,ct).ConfigureAwait(false));
   if(html.IndexOf("anomaly.js",StringComparison.OrdinalIgnoreCase)>=0||html.IndexOf("challenge-form",StringComparison.OrdinalIgnoreCase)>=0)throw new LookupBlockedException("DuckDuckGo needs a browser check before more thumbnails can be fetched.");
   Match token=Regex.Match(html,"vqd=['\"](?<token>[0-9-]+)['\"]");
   if(!token.Success)throw new InvalidDataException("DuckDuckGo did not provide image search data.");
   string endpoint="https://duckduckgo.com/i.js?l=uk-en&o=json&q="+Uri.EscapeDataString(query)+"&vqd="+Uri.EscapeDataString(token.Groups["token"].Value)+"&p=1";
   string json=Encoding.UTF8.GetString(await Get(endpoint,2*1024*1024,ct).ConfigureAwait(false));
   var payload=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<Dictionary<string,object>>(json);
   object rows;if(payload==null||!payload.TryGetValue("results",out rows))throw new InvalidDataException("No image results.");
   var items=rows as System.Collections.IEnumerable;if(items==null)throw new InvalidDataException("Invalid image results.");
   int tried=0;
   foreach(object item in items){
    ct.ThrowIfCancellationRequested();var row=item as Dictionary<string,object>;if(row==null)continue;if(++tried>8)break;
    string image=Value(row,"thumbnail");if(image.Length==0)image=Value(row,"image");if(image.Length==0)continue;
    try{
     Image framed=DecodeAndFrame(await Get(image,5*1024*1024,ct).ConfigureAwait(false));
     string source=Value(row,"url");try{Store(title,framed,source);}catch{}
     return new OnlineThumbnailResult{Image=framed,Source=source};
    }catch(OperationCanceledException){throw;}catch(LookupBlockedException){throw;}catch{}
   }
   return new OnlineThumbnailResult{Error="No usable search-engine thumbnail found."};
  }catch(LookupBlockedException ex){blockedUntil=DateTime.UtcNow.AddMinutes(10);return new OnlineThumbnailResult{Error=ex.Message,TemporarilyBlocked=true};}
   catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;return new OnlineThumbnailResult{Error="Thumbnail lookup timed out."};}
   catch(Exception ex){return new OnlineThumbnailResult{Error=ex.Message};}
   finally{gate.Release();}
 }
}
}
