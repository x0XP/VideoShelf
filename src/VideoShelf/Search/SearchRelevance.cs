using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VideoShelf {
static class SearchRelevance {
 static readonly HashSet<string> StopWords=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
  "a","an","the","and","or","of","to","in","on","for","with","from","by","at","is","are","am","i","my","this","that","season","episode"
 };

 public static List<OnlineResult> FilterAndRank(string query,IEnumerable<OnlineResult> rows){
  return (rows??Enumerable.Empty<OnlineResult>())
   .Select(r=>new { Result=r, Score=Score(query,r==null?"":r.Title) })
   .Where(x=>x.Result!=null&&x.Score>0)
   .OrderByDescending(x=>x.Score)
   .ThenByDescending(x=>x.Result.Seeders)
   .ThenByDescending(x=>x.Result.Published)
   .ThenBy(x=>x.Result.Title,StringComparer.OrdinalIgnoreCase)
   .Select(x=>x.Result).ToList();
 }

 public static int Score(string query,string title){
  string[] q=Tokens(query,true),t=Tokens(title,false);
  if(q.Length==0||t.Length==0)return 0;
  int matches=0,quality=0;
  foreach(string token in q){
   int best=0;
   foreach(string candidate in t){
    if(token.Equals(candidate,StringComparison.OrdinalIgnoreCase)){best=100;break;}
    if(token.Length>=4&&(candidate.StartsWith(token,StringComparison.OrdinalIgnoreCase)||token.StartsWith(candidate,StringComparison.OrdinalIgnoreCase)))best=Math.Max(best,82);
    if(token.Length>=4&&(candidate.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0||token.IndexOf(candidate,StringComparison.OrdinalIgnoreCase)>=0))best=Math.Max(best,76);
    int tolerance=token.Length>=8?2:token.Length>=4?1:0;
    if(tolerance>0&&Math.Abs(token.Length-candidate.Length)<=tolerance&&EditDistanceWithin(token,candidate,tolerance))best=Math.Max(best,tolerance==1?72:64);
   }
   if(best>0){matches++;quality+=best;}
  }

  // Short searches are only useful when every meaningful term is represented.
  // Longer searches may contain an extra descriptive word, but still need strong coverage.
  int required=q.Length<=3?q.Length:(int)Math.Ceiling(q.Length*0.70);
  if(matches<required)return 0;

  string nq=Normalize(query),nt=Normalize(title);
  int phrase=nt.IndexOf(nq,StringComparison.OrdinalIgnoreCase)>=0?80:0;
  int coverage=(int)Math.Round(matches*100d/q.Length);
  return quality+coverage+phrase;
 }

 static string[] Tokens(string value,bool query){
  return Normalize(value).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)
   .Where(x=>!query||(!StopWords.Contains(x)&&(!x.All(char.IsDigit)||x.Length>=4)))
   .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }

 static string Normalize(string value){return Regex.Replace((value??"").ToLowerInvariant(),@"[^\p{L}\p{N}]+"," ").Trim();}

 static bool EditDistanceWithin(string a,string b,int max){
  if(Math.Abs(a.Length-b.Length)>max)return false;
  int[] prev=Enumerable.Range(0,b.Length+1).ToArray(),cur=new int[b.Length+1];
  for(int i=1;i<=a.Length;i++){
   cur[0]=i;int rowMin=cur[0];
   for(int j=1;j<=b.Length;j++){
    int cost=a[i-1]==b[j-1]?0:1;
    cur[j]=Math.Min(Math.Min(cur[j-1]+1,prev[j]+1),prev[j-1]+cost);rowMin=Math.Min(rowMin,cur[j]);
   }
   if(rowMin>max)return false;
   var swap=prev;prev=cur;cur=swap;
  }
  return prev[b.Length]<=max;
 }

 public static void SelfTest(){
  string relevant="Though I Am an Inept Villainess S01E09 1080p";
  string unrelated="[SubsPlease] Hell Mode S2 - 11 (1080p)";
  if(Score("innept villainess",relevant)<=0)throw new InvalidOperationException("Search relevance did not tolerate a one-character query typo.");
  if(Score("innept villainess",unrelated)!=0)throw new InvalidOperationException("Search relevance accepted an unrelated high-seeder title.");
  if(Score("re zero","Re:ZERO - Starting Life in Another World S04E16 1080p")<=0)throw new InvalidOperationException("Search relevance failed punctuation-normalised title matching.");
  var ranked=FilterAndRank("innept villainess",new[]{
   new OnlineResult{Title=unrelated,Seeders=5000},
   new OnlineResult{Title=relevant,Seeders=25}
  });
  if(ranked.Count!=1||ranked[0].Title!=relevant)throw new InvalidOperationException("Search relevance did not outrank/filter by query relevance before seed count.");
 }
}
}
