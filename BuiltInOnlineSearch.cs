using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoShelf {
static class BuiltInOnlineSearch {
 sealed class SourceResult { public bool Failed; public List<OnlineResult> Results=new List<OnlineResult>(); }
 static readonly string[] Sources={
  "https://nyaa.si/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.media/?page=rss&q={query}&c=1_0&f=0",
  "https://nyaa.mom/?page=rss&q={query}&c=1_0&f=0",
  "https://www.torlock2.com/torznab/api",
  "https://stremthrufortheweebs.midnightignite.me/v0/torznab/api"
 };

 public static async Task<List<OnlineResult>> Search(string query,CancellationToken ct){
  string normalized=Regex.Replace((query??"").Trim(),@"[^\p{L}\p{N}]+"," ").Trim();
  if(normalized.Length==0)normalized=(query??"").Trim();
  var tasks=Sources.Select(source=>SearchSource(source,normalized,ct)).ToArray();
  SourceResult[] responses=await Task.WhenAll(tasks);
  ct.ThrowIfCancellationRequested();
  var combined=responses.SelectMany(r=>r.Results).Where(r=>r!=null&&r.Seeders>0&&!string.IsNullOrWhiteSpace(r.Link)).ToList();
  var result=combined
   .GroupBy(r=>string.IsNullOrWhiteSpace(r.Link)?r.Title:r.Link,StringComparer.OrdinalIgnoreCase)
   .Select(g=>g.OrderByDescending(r=>r.Seeders).First())
   .OrderByDescending(r=>r.Seeders).ThenByDescending(r=>r.Published).ThenBy(r=>r.Title,StringComparer.OrdinalIgnoreCase).Take(80).ToList();
  if(result.Count==0&&responses.Length>0&&responses.All(r=>r.Failed))throw new InvalidOperationException("Built-in metadata sources are currently unavailable. You can retry or configure a custom source in Settings.");
  return result;
 }

 static async Task<SourceResult> SearchSource(string source,string query,CancellationToken parent){
  var response=new SourceResult();
  using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(parent)){
   timeout.CancelAfter(TimeSpan.FromSeconds(10));
   try{
    var found=await TorznabSearch.Search(query,new OnlineSettings{Url=source,ApiKey="",AutoSearch=true},timeout.Token);
    if(found!=null){
     Uri uri;string probe=source.Replace("{query}","search");string fallback=Uri.TryCreate(probe,UriKind.Absolute,out uri)?uri.Host:"Built-in";
     foreach(var row in found.Where(r=>r!=null&&r.Seeders>0)){
      if(string.IsNullOrWhiteSpace(row.Source)||row.Source.Equals("Indexer",StringComparison.OrdinalIgnoreCase))row.Source=fallback;
      response.Results.Add(row);
     }
    }
   }catch(OperationCanceledException){if(parent.IsCancellationRequested)throw;response.Failed=true;}
   catch{response.Failed=true;}
  }
  return response;
 }
}
}
