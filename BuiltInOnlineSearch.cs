using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VideoShelf {
static class BuiltInOnlineSearch {
 static readonly string[] Sources={
  "https://nyaa.si/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.media/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.mom/?page=rss&q={query}&c=1_0&f=0",
  "https://www.torlock2.com/torznab/api",
  "https://stremthrufortheweebs.midnightignite.me/v0/torznab/api"
 };

 public static async Task<List<OnlineResult>> Search(string query,CancellationToken ct){
  var combined=new List<OnlineResult>();int attempted=0,failed=0;
  foreach(string source in Sources){
   ct.ThrowIfCancellationRequested();attempted++;
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct)){
    timeout.CancelAfter(TimeSpan.FromSeconds(9));
    try{
     var found=await TorznabSearch.Search(query,new OnlineSettings{Url=source,ApiKey="",AutoSearch=true},timeout.Token);
     if(found!=null)combined.AddRange(found.Where(r=>r!=null&&r.Seeders>0));
    }catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;failed++;}
    catch{failed++;}
   }
   if(combined.Count>=40)break;
  }
  ct.ThrowIfCancellationRequested();
  var result=combined.Where(r=>r.Seeders>0&& !string.IsNullOrWhiteSpace(r.Link))
   .GroupBy(r=>string.IsNullOrWhiteSpace(r.Link)?r.Title:r.Link,StringComparer.OrdinalIgnoreCase)
   .Select(g=>g.OrderByDescending(r=>r.Seeders).First())
   .OrderByDescending(r=>r.Seeders).ThenByDescending(r=>r.Published).ThenBy(r=>r.Title,StringComparer.OrdinalIgnoreCase).Take(80).ToList();
  if(result.Count==0&&attempted>0&&failed==attempted)throw new InvalidOperationException("Built-in metadata sources are currently unavailable. You can retry or configure a custom source in Settings.");
  return result;
 }
}
}
