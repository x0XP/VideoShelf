using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VideoShelf {
static class BuiltInOnlineSearch {
 sealed class SourceResult { public bool Failed; public List<OnlineResult> Results=new List<OnlineResult>(); }
 static readonly HttpClient http=CreateHttp();
 static readonly string[] Sources={
  "https://nyaa.si/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.media/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.mom/?page=rss&q={query}&c=1_0&f=0",
  "https://www.torlock2.com/torznab/api",
  "https://stremthrufortheweebs.midnightignite.me/v0/torznab/api"
 };

 static HttpClient CreateHttp(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var handler=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
  return client;
 }

 public static async Task<List<OnlineResult>> Search(string query,CancellationToken ct){
  ct.ThrowIfCancellationRequested();
  string normalized=Regex.Replace((query??"").Trim(),@"[^\p{L}\p{N}]+"," ").Trim();
  if(normalized.Length==0)normalized=(query??"").Trim();

  var responses=new List<SourceResult>();
  bool deadlineReached=false;

  // Keep the aggregate deadline independent from request cancellation. On the
  // .NET Framework HttpClient stack a cancellation callback can itself block
  // while a DNS/TLS request is wedged. The UI must never wait for that cleanup.
  var pending=Sources
   .Select(source=>Task.Run(()=>SearchSource(source,normalized)))
   .Concat(new[]{Task.Run(()=>SearchApiBay(normalized))})
   .ToList();
  Task deadline=Task.Delay(TimeSpan.FromSeconds(11));
  Task cancelled=Task.Delay(Timeout.Infinite,ct);

  while(pending.Count>0){
   Task completed=await Task.WhenAny(pending.Select(t=>(Task)t).Concat(new[]{deadline,cancelled})).ConfigureAwait(false);
   if(object.ReferenceEquals(completed,cancelled)){
    ct.ThrowIfCancellationRequested();
   }
   if(object.ReferenceEquals(completed,deadline)){
    deadlineReached=true;
    break;
   }
   var finished=(Task<SourceResult>)completed;
   pending.Remove(finished);
   try{responses.Add(await finished.ConfigureAwait(false));}
   catch(OperationCanceledException){responses.Add(new SourceResult{Failed=true});}
   catch{responses.Add(new SourceResult{Failed=true});}
  }

  ct.ThrowIfCancellationRequested();
  var combined=responses.SelectMany(r=>r.Results).Where(r=>r!=null&&r.Seeders>0&&!string.IsNullOrWhiteSpace(r.Link)).ToList();
  var result=combined
   .GroupBy(r=>string.IsNullOrWhiteSpace(r.Link)?r.Title:r.Link,StringComparer.OrdinalIgnoreCase)
   .Select(g=>g.OrderByDescending(r=>r.Seeders).First())
   .OrderByDescending(r=>r.Seeders).ThenByDescending(r=>r.Published).ThenBy(r=>r.Title,StringComparer.OrdinalIgnoreCase).Take(80).ToList();
  if(result.Count==0&&((responses.Count==0&&deadlineReached)||(responses.Count>0&&responses.All(r=>r.Failed))))
   throw new InvalidOperationException("Built-in metadata sources are currently unavailable. You can retry or configure a custom source in Settings.");
  return result;
 }

 public static void SelfTest(){
  const string sample="[{\"id\":\"123\",\"name\":\"VideoShelf Test S01 1080p\",\"info_hash\":\"0123456789abcdef0123456789abcdef01234567\",\"seeders\":\"42\",\"leechers\":\"7\",\"size\":\"1073741824\",\"added\":\"1700000000\"},{\"id\":\"124\",\"name\":\"Zero Seeder Must Be Hidden\",\"info_hash\":\"abcdef0123456789abcdef0123456789abcdef01\",\"seeders\":\"0\",\"leechers\":\"1\",\"size\":\"1000\",\"added\":\"1700000001\"}]";
  var rows=ParseApiBayPayload(sample,CancellationToken.None);
  if(rows.Count!=1)throw new InvalidOperationException("Built-in metadata parser did not enforce the zero-seeder invariant.");
  var row=rows[0];
  if(row.Seeders!=42||row.Leechers!=7||row.Size!=1073741824L||row.Resolution!="1080p"||!row.Link.StartsWith("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567",StringComparison.OrdinalIgnoreCase))
   throw new InvalidOperationException("Built-in metadata parser self-test failed.");
 }

 static async Task<SourceResult> SearchSource(string source,string query){
  var response=new SourceResult();
  using(var timeout=new CancellationTokenSource()){
   timeout.CancelAfter(TimeSpan.FromSeconds(10));
   try{
    var found=await TorznabSearch.Search(query,new OnlineSettings{Url=source,ApiKey="",AutoSearch=true},timeout.Token).ConfigureAwait(false);
    if(found!=null){
     Uri uri;string probe=source.Replace("{query}","search");string fallback=Uri.TryCreate(probe,UriKind.Absolute,out uri)?uri.Host:"Built-in";
     foreach(var row in found.Where(r=>r!=null&&r.Seeders>0)){
      if(string.IsNullOrWhiteSpace(row.Source)||row.Source.Equals("Indexer",StringComparison.OrdinalIgnoreCase))row.Source=fallback;
      response.Results.Add(row);
     }
    }
   }catch(OperationCanceledException){response.Failed=true;}
   catch{response.Failed=true;}
  }
  return response;
 }

 static async Task<SourceResult> SearchApiBay(string query){
  var response=new SourceResult();
  using(var timeout=new CancellationTokenSource()){
   timeout.CancelAfter(TimeSpan.FromSeconds(10));
   try{
    string url="https://apibay.org/q.php?q="+Uri.EscapeDataString(query)+"&cat=200";
    string payload;
    using(var message=await http.GetAsync(url,HttpCompletionOption.ResponseContentRead,timeout.Token).ConfigureAwait(false)){
     if(!message.IsSuccessStatusCode){response.Failed=true;return response;}
     payload=await message.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
    timeout.Token.ThrowIfCancellationRequested();
    response.Results=ParseApiBayPayload(payload,timeout.Token);
   }catch(OperationCanceledException){response.Failed=true;}
   catch{response.Failed=true;}
  }
  return response;
 }

 static List<OnlineResult> ParseApiBayPayload(string payload,CancellationToken ct){
  var results=new List<OnlineResult>();
  var serializer=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
  var rows=serializer.DeserializeObject(payload) as object[];
  if(rows==null)return results;
  foreach(object raw in rows){
   ct.ThrowIfCancellationRequested();
   var item=raw as Dictionary<string,object>;if(item==null)continue;
   string title=StringValue(item,"name");if(title.Length==0)continue;
   int seeders=IntValue(item,"seeders");if(seeders<=0)continue;
   string hash=StringValue(item,"info_hash");if(!Regex.IsMatch(hash,@"^[A-Fa-f0-9]{40}$"))continue;
   int leechers=IntValue(item,"leechers");
   long size=LongValue(item,"size");
   long added=LongValue(item,"added");DateTime published=DateTime.MinValue;
   if(added>0)try{published=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(added);}catch{}
   string id=StringValue(item,"id");
   string page=id.Length>0?"https://thepiratebay.org/description.php?id="+Uri.EscapeDataString(id):"";
   string link="magnet:?xt=urn:btih:"+hash+"&dn="+Uri.EscapeDataString(title);
   results.Add(new OnlineResult{Title=title,Link=link,PageUrl=page,Source="apibay.org",Resolution=DetectResolution(title),Size=size,Seeders=seeders,Leechers=leechers,Published=published});
  }
  return results;
 }

 static object Value(Dictionary<string,object> item,string key){object value;return item!=null&&item.TryGetValue(key,out value)?value:null;}
 static string StringValue(Dictionary<string,object> item,string key){object value=Value(item,key);return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture).Trim();}
 static int IntValue(Dictionary<string,object> item,string key){int n;return int.TryParse(StringValue(item,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out n)?Math.Max(0,n):0;}
 static long LongValue(Dictionary<string,object> item,string key){long n;return long.TryParse(StringValue(item,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out n)?Math.Max(0,n):0;}
 static string DetectResolution(string title){
  Match m=Regex.Match(title??"",@"(?<!\d)(2160|1080|720|576|540|480|360)p\b",RegexOptions.IgnoreCase);if(m.Success)return m.Groups[1].Value+"p";
  return Regex.IsMatch(title??"",@"\b(4k|uhd)\b",RegexOptions.IgnoreCase)?"2160p":"Other";
 }
}
}