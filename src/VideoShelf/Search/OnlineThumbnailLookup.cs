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
 sealed class ThumbnailIdentity {
  public string Series="", EpisodeLabel="", CacheIdentity="";
  public bool Episodic;
  public int Season=-1, Episode=-1;
 }
 sealed class ThumbnailCandidate {
  public Dictionary<string,object> Row;
  public int Score;
 }

 static DateTime blockedUntil=DateTime.MinValue;
 static readonly HttpClient http=CreateClient();
 static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
 static readonly string cache=AppDataPaths.MigrateDirectory("OnlineThumbnails");
 static readonly string[] stopWords={"the","a","an","and","or","of","in","on","to","for","from","with","part","season"};

 static HttpClient CreateClient(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var h=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var c=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15)};
  c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
  return c;
 }

 static string CleanTechnicalNoise(string value){
  string s=value??"";
  s=Regex.Replace(s,@"\.(torrent|mkv|mp4|avi|mov|wmv|webm|m4v)$"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"(?<![A-Za-z0-9])(2160p|1080p|720p|576p|540p|480p|360p|4k|uhd|fhd|hdr10\+?|hdr|dolby[ ._-]?vision|dv|x264|x265|h\.?264|h\.?265|hevc|av1|10bit|8bit|bluray|blu[ ._-]?ray|bdrip|brrip|web[ ._-]?dl|webrip|webcap|hdtv|dvdrip|remux|aac(?:2\.0|5\.1)?|ac3|eac3|ddp(?:2\.0|5\.1)?|dts(?:hd)?|truehd|atmos|flac|mp3|proper|repack|rerip|internal|limited|extended|uncut|multi|dual[ ._-]?audio)(?![A-Za-z0-9])"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"(?<![A-Za-z0-9])(?:\d{1,2}bit|\d{3,4}kbps|\d{2,3}fps)(?![A-Za-z0-9])"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"\[[A-Fa-f0-9]{6,12}\]"," ");
  s=Regex.Replace(s,@"[._]+"," ");
  s=Regex.Replace(s,@"[\[\]\(\)\{\}]"," ");
  s=Regex.Replace(s,@"\s+"," ").Trim(' ','-','_','.');
  return s;
 }

 static string CleanSeries(string value){
  string s=CleanTechnicalNoise(value);
  s=Regex.Replace(s,@"\s+-\s*[A-Za-z0-9][A-Za-z0-9._-]{1,20}\s*$"," ");
  s=Regex.Replace(s,@"\s+"," ").Trim(' ','-','_','.');
  if(s.Length>120)s=s.Substring(0,120).Trim();
  return s;
 }

 static ThumbnailIdentity Identify(string title){
  string raw=(title??"").Trim();
  string withoutGroup=Regex.Replace(raw,@"^\s*(?:\[[^\]\r\n]{1,48}\]\s*)+"," ").Trim();
  string probe=withoutGroup;
  Match m=Regex.Match(probe,@"(?<![A-Za-z0-9])S(?<s>\d{1,2})E(?<e>\d{1,3})(?!\d)",RegexOptions.IgnoreCase);
  if(!m.Success)m=Regex.Match(probe,@"(?<![A-Za-z0-9])(?<s>\d{1,2})x(?<e>\d{1,3})(?!\d)",RegexOptions.IgnoreCase);
  if(m.Success){
   int season,episode;if(int.TryParse(m.Groups["s"].Value,out season)&&int.TryParse(m.Groups["e"].Value,out episode)){
    string series=CleanSeries(probe.Substring(0,m.Index));
    if(series.Length>0)return new ThumbnailIdentity{Series=series,Episodic=true,Season=season,Episode=episode,EpisodeLabel="season "+season+" episode "+episode,CacheIdentity=series.ToLowerInvariant()+"|s"+season+"e"+episode};
   }
  }
  m=Regex.Match(probe,@"(?<![A-Za-z0-9])(?:episode|ep)\s*\.?\s*(?<e>\d{1,4})(?:v\d+)?\b",RegexOptions.IgnoreCase);
  if(!m.Success)m=Regex.Match(probe,@"\s+-\s*(?:episode\s*)?(?<e>\d{1,4})(?:v\d+)?\b",RegexOptions.IgnoreCase);
  if(m.Success){
   int episode;if(int.TryParse(m.Groups["e"].Value,out episode)){
    string series=CleanSeries(probe.Substring(0,m.Index));
    if(series.Length>0)return new ThumbnailIdentity{Series=series,Episodic=true,Episode=episode,EpisodeLabel="episode "+episode,CacheIdentity=series.ToLowerInvariant()+"|e"+episode};
   }
  }
  string generic=CleanSeries(withoutGroup);
  if(generic.Length==0)generic=raw;
  return new ThumbnailIdentity{Series=generic,CacheIdentity=generic.ToLowerInvariant()};
 }

 static string CleanContext(string context){string s=Regex.Replace((context??"").Trim(),@"\s+"," ");return s.Length>120?s.Substring(0,120).Trim():s;}
 static string ContextPrefix(string context){string s=CleanContext(context).Replace("\""," ").Trim();return s.Length==0?"":"\""+s+"\" ";}
 static bool ContextAlreadyPresent(ThumbnailIdentity id,string context){string c=NormalizedWords(CleanContext(context)),series=NormalizedWords(id.Series);return c.Length>0&&series.Contains(c);}
 static string PrimaryQuery(ThumbnailIdentity id,string context){string prefix=ContextAlreadyPresent(id,context)?"":ContextPrefix(context);return prefix+(id.Episodic?id.Series+" "+id.EpisodeLabel+" screenshot":id.Series);}
 static string SecondaryQuery(ThumbnailIdentity id,string context){string prefix=ContextAlreadyPresent(id,context)?"":ContextPrefix(context);return prefix+(id.Episodic?id.Series+" "+id.EpisodeLabel+" still":id.Series+" image");}
 static string PrimaryQuery(ThumbnailIdentity id){return PrimaryQuery(id,"");}
 static string SecondaryQuery(ThumbnailIdentity id){return SecondaryQuery(id,"");}
 public static string SearchText(string title){return PrimaryQuery(Identify(title));}
 public static string SearchText(string title,string context){return PrimaryQuery(Identify(title),context);}

 public static string CacheKey(string title){return CacheKey(title,"");}
 public static string CacheKey(string title,string context){
  string key="v4|"+NormalizedWords(CleanContext(context))+"|"+Identify(title).CacheIdentity;
  using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","");
 }

 static string FileFor(string title,string context){return Path.Combine(cache,CacheKey(title,context)+".jpg");}
 static string ForceSafeOff(string query){string q=(query??"").Trim();return q.IndexOf("!safeoff",StringComparison.OrdinalIgnoreCase)>=0?q:q+" !safeoff";}
 static string SearchUrlFor(string query){query=ForceSafeOff(query);return "https://duckduckgo.com/?q="+Uri.EscapeDataString(query)+"&iax=images&ia=images&kp=-2";}
 public static string SearchUrl(string title){return SearchUrlFor(SearchText(title));}
 public static string SearchUrl(string title,string context){return SearchUrlFor(SearchText(title,context));}

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
 static string NormalizedWords(string value){return Regex.Replace((value??"").ToLowerInvariant(),@"[^a-z0-9]+"," ").Trim();}
 static string[] SignificantWords(string series){return Regex.Matches(series??"",@"[A-Za-z0-9]+",RegexOptions.IgnoreCase).Cast<Match>().Select(x=>x.Value.ToLowerInvariant()).Where(x=>x.Length>1&&!stopWords.Contains(x)).Distinct().ToArray();}
 static bool ContainsEpisodeReference(string text,ThumbnailIdentity id){
  if(!id.Episodic)return true;
  string lower=(text??"").ToLowerInvariant();
  if(id.Season>=0){
   string compact="s"+id.Season.ToString("00")+"e"+id.Episode.ToString("00");
   if(lower.Contains(compact))return true;
   if(Regex.IsMatch(lower,@"\bseason\s*0*"+id.Season+@"\D{0,12}episode\s*0*"+id.Episode+@"\b",RegexOptions.IgnoreCase))return true;
  }
  return Regex.IsMatch(lower,@"\b(?:episode|ep)\s*[#:_\-. ]*0*"+id.Episode+@"\b",RegexOptions.IgnoreCase) || Regex.IsMatch(lower,@"(?:^|[/_\-. ])0*"+id.Episode+@"(?:[/_\-. ]|$)",RegexOptions.IgnoreCase);
 }
 static int CandidateScore(Dictionary<string,object> row,ThumbnailIdentity id,string context){
  string title=Value(row,"title"),url=Value(row,"url"),image=Value(row,"image");
  string combined=title+" "+url+" "+image;
  string normalized=NormalizedWords(combined);
  string[] words=SignificantWords(id.Series);int matched=0;
  foreach(string word in words)if(normalized.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)matched++;
  int minimum=words.Length<=1?words.Length:Math.Min(2,words.Length);
  if(matched<minimum)return int.MinValue;
  if(id.Episodic&&!ContainsEpisodeReference(combined,id))return int.MinValue;
  int score=matched*5;
  string cleanContext=CleanContext(context),contextPhrase=NormalizedWords(cleanContext);
  if(contextPhrase.Length>0){
   string[] contextWords=SignificantWords(cleanContext);int contextMatched=0;
   foreach(string word in contextWords)if(normalized.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)contextMatched++;
   score+=contextMatched*12;if(normalized.Contains(contextPhrase))score+=24;
  }
  if(id.Episodic)score+=30;
  if(Regex.IsMatch(combined,@"\b(screenshot|screencap|screen\s*cap|still|gallery|recap|review)\b",RegexOptions.IgnoreCase))score+=8;
  if(Regex.IsMatch(combined,@"\b(poster|wallpaper|key\s*visual|promotional|promo|cover|box\s*art)\b",RegexOptions.IgnoreCase))score-=14;
  string seriesWords=NormalizedWords(id.Series);
  if(seriesWords.Length>0&&normalized.Contains(seriesWords))score+=10;
  return score;
 }

 static async Task<List<Dictionary<string,object>>> SearchRows(string query,CancellationToken ct){
  string effectiveQuery=ForceSafeOff(query);
  string html=Encoding.UTF8.GetString(await Get(SearchUrlFor(effectiveQuery),2*1024*1024,ct).ConfigureAwait(false));
  if(html.IndexOf("anomaly.js",StringComparison.OrdinalIgnoreCase)>=0||html.IndexOf("challenge-form",StringComparison.OrdinalIgnoreCase)>=0)throw new LookupBlockedException("DuckDuckGo needs a browser check before more thumbnails can be fetched.");
  Match token=Regex.Match(html,"vqd=['\"](?<token>[0-9-]+)['\"]");
  if(!token.Success)throw new InvalidDataException("DuckDuckGo did not provide image search data.");
  string endpoint="https://duckduckgo.com/i.js?l=uk-en&o=json&q="+Uri.EscapeDataString(effectiveQuery)+"&vqd="+Uri.EscapeDataString(token.Groups["token"].Value)+"&p=-2&kp=-2";
  string json=Encoding.UTF8.GetString(await Get(endpoint,2*1024*1024,ct).ConfigureAwait(false));
  var payload=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<Dictionary<string,object>>(json);
  object rows;if(payload==null||!payload.TryGetValue("results",out rows))return new List<Dictionary<string,object>>();
  var items=rows as System.Collections.IEnumerable;if(items==null)return new List<Dictionary<string,object>>();
  return items.Cast<object>().Select(x=>x as Dictionary<string,object>).Where(x=>x!=null).ToList();
 }

 static void Store(string title,string context,Image image,string source){
  Directory.CreateDirectory(cache);string f=FileFor(title,context),temp=f+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{image.Save(temp,ImageFormat.Jpeg);if(File.Exists(f))File.Delete(f);File.Move(temp,f);File.WriteAllText(f+".source",source??"");}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }

 static async Task<OnlineThumbnailResult> FindFromQuery(string title,string context,ThumbnailIdentity id,string query,CancellationToken ct){
  List<Dictionary<string,object>> rows=await SearchRows(query,ct).ConfigureAwait(false);
  var ranked=rows.Select(row=>new ThumbnailCandidate{Row=row,Score=CandidateScore(row,id,context)}).Where(x=>x.Score>int.MinValue).OrderByDescending(x=>x.Score).Take(10).ToArray();
  foreach(var candidate in ranked){
   ct.ThrowIfCancellationRequested();string image=Value(candidate.Row,"image");if(image.Length==0)image=Value(candidate.Row,"thumbnail");if(image.Length==0)continue;
   try{
    Image framed=DecodeAndFrame(await Get(image,5*1024*1024,ct).ConfigureAwait(false));
    string source=Value(candidate.Row,"url");try{Store(title,context,framed,source);}catch{}
    return new OnlineThumbnailResult{Image=framed,Source=source};
   }catch(OperationCanceledException){throw;}catch(LookupBlockedException){throw;}catch{}
  }
  return null;
 }

 public static Task<OnlineThumbnailResult> Find(string title,CancellationToken ct){return Find(title,"",ct);}
 public static async Task<OnlineThumbnailResult> Find(string title,string context,CancellationToken ct){
  await gate.WaitAsync(ct).ConfigureAwait(false);
  try{
   context=CleanContext(context);ct.ThrowIfCancellationRequested();string f=FileFor(title,context);
   if(File.Exists(f))try{return new OnlineThumbnailResult{Image=DecodeAndFrame(File.ReadAllBytes(f)),FromCache=true,Source=File.Exists(f+".source")?File.ReadAllText(f+".source"):"Cached search result"};}catch{}
   if(DateTime.UtcNow<blockedUntil)return new OnlineThumbnailResult{Error="Thumbnail lookup is temporarily paused after a search-engine access check.",TemporarilyBlocked=true};
   await Task.Delay(850,ct).ConfigureAwait(false);
   ThumbnailIdentity id=Identify(title);
   OnlineThumbnailResult result=await FindFromQuery(title,context,id,PrimaryQuery(id,context),ct).ConfigureAwait(false);
   if(result==null&&id.Episodic){await Task.Delay(350,ct).ConfigureAwait(false);result=await FindFromQuery(title,context,id,SecondaryQuery(id,context),ct).ConfigureAwait(false);}
   if(result==null&&!id.Episodic){await Task.Delay(350,ct).ConfigureAwait(false);result=await FindFromQuery(title,context,id,SecondaryQuery(id,context),ct).ConfigureAwait(false);}
   if(result!=null)return result;
   return new OnlineThumbnailResult{Error=id.Episodic?"No episode-specific search-engine thumbnail found.":"No usable search-engine thumbnail found."};
  }catch(LookupBlockedException ex){blockedUntil=DateTime.UtcNow.AddMinutes(10);return new OnlineThumbnailResult{Error=ex.Message,TemporarilyBlocked=true};}
   catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;return new OnlineThumbnailResult{Error="Thumbnail lookup timed out."};}
   catch(Exception ex){return new OnlineThumbnailResult{Error=ex.Message};}
   finally{gate.Release();}
 }

 public static void SelfTest(){
  string anime="[SubsPlease] Re Zero kara Hajimeru Isekai Seikatsu - 82 (1080p) [8F1D0883].mkv";
  string animeSearch=SearchText(anime);
  if(animeSearch.IndexOf("episode 82",StringComparison.OrdinalIgnoreCase)<0||animeSearch.IndexOf("screenshot",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Anime episode thumbnail query lost its episode identity.");
  if(CacheKey(anime)==CacheKey("[SubsPlease] Re Zero kara Hajimeru Isekai Seikatsu - 83 (1080p).mkv"))throw new InvalidOperationException("Episode thumbnails share a cache key.");
  string tv=SearchText("Example.Show.S02E07.1080p.WEB-DL.x265.mkv");
  if(tv.IndexOf("season 2 episode 7",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("TV episode thumbnail query lost its season/episode identity.");
  string contextual=SearchText("Sample.Release.1080p.mkv","Sample Subject");
  if(contextual.IndexOf("Sample Subject",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Search context was not preserved for thumbnail lookup.");
  if(CacheKey("Sample.Release.1080p.mkv","Sample Subject")==CacheKey("Sample.Release.1080p.mkv","Different Subject"))throw new InvalidOperationException("Contextual thumbnail cache keys collided.");
  if(ForceSafeOff("sample query").IndexOf("!safeoff",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Safe Search override token is missing.");
 }
}
}
