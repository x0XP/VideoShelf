using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VideoShelf {
sealed class LibraryMediaInfo {
 public bool IsEpisode;
 public int Season=-1,Episode=-1;
 public string Series="",EpisodeTitle="",EpisodeCode="",GroupLabel="Other videos";
}

static class LibraryMediaIntelligence {
 static readonly Regex Technical=new Regex(@"\b(?:2160p|1440p|1080p|720p|576p|540p|480p|360p|4k|uhd|fhd|hdr10\+?|hdr|dv|dolby[ ._-]?vision|x264|x265|h\.?264|h\.?265|hevc|av1|10bit|8bit|bluray|blu[ ._-]?ray|bdrip|brrip|web[ ._-]?dl|webrip|webcap|hdtv|dvdrip|remux|aac|ac3|eac3|ddp|dts|truehd|atmos|flac|opus|mp3|proper|repack|rerip|internal|limited|extended|uncut|multi|dual[ ._-]?audio)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);

 public static LibraryMediaInfo Analyze(string fileName,string collectionName,string relative){
  var info=new LibraryMediaInfo();
  string stem=Path.GetFileNameWithoutExtension(fileName??"");
  string probe=Regex.Replace(stem,@"^\s*(?:\[[^\]\r\n]{1,64}\]\s*)+"," ").Trim();
  Match match=Regex.Match(probe,@"(?<![A-Za-z0-9])S(?<s>\d{1,2})[ ._-]*E(?<e>\d{1,3})(?!\d)",RegexOptions.IgnoreCase);
  if(!match.Success)match=Regex.Match(probe,@"(?<![A-Za-z0-9])(?<s>\d{1,2})x(?<e>\d{1,3})(?!\d)",RegexOptions.IgnoreCase);
  bool explicitSeason=match.Success;
  if(!match.Success)match=Regex.Match(probe,@"(?<![A-Za-z0-9])(?:episode|ep)[ ._-]*(?<e>\d{1,4})(?:v\d+)?\b",RegexOptions.IgnoreCase);
  if(!match.Success)match=Regex.Match(probe,@"\s-\s(?:episode\s*)?(?<e>\d{1,3})(?:v\d+)?(?:\s|$)",RegexOptions.IgnoreCase);
  if(!match.Success)return info;

  int episode;
  if(!int.TryParse(match.Groups["e"].Value,out episode)||episode<0)return info;
  int season=-1;
  if(explicitSeason)int.TryParse(match.Groups["s"].Value,out season);
  if(season<0){
   Match folderSeason=Regex.Match(relative??"",@"(?:^|[\\/])(?:season|s)[ ._-]*0*(?<s>\d{1,2})(?:$|[\\/])",RegexOptions.IgnoreCase);
   int parsed;if(folderSeason.Success&&int.TryParse(folderSeason.Groups["s"].Value,out parsed))season=parsed;
  }

  string series=Clean(probe.Substring(0,match.Index));
  if(series.Length<2)series=Clean(collectionName);
  string tail=match.Index+match.Length<probe.Length?probe.Substring(match.Index+match.Length):"";
  string episodeTitle=Clean(tail);
  if(episodeTitle.Equals(series,StringComparison.OrdinalIgnoreCase))episodeTitle="";

  info.IsEpisode=true;info.Season=season;info.Episode=episode;info.Series=series;
  info.EpisodeTitle=episodeTitle;
  info.EpisodeCode=season>=0?"S"+season.ToString("00")+"E"+episode.ToString("00"):"E"+episode.ToString("00");
  info.GroupLabel=season>=0?"Season "+season:"Episodes";
  return info;
 }

 static string Clean(string value){
  string s=value??"";
  s=Regex.Replace(s,@"\[[^\]]*\]|\([^\)]*\)"," ");
  s=Technical.Replace(s," ");
  s=Regex.Replace(s,@"\b(?:\d{2,3}fps|\d{3,4}kbps|\d{1,2}bit)\b"," ",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"\[[A-Fa-f0-9]{6,12}\]"," ");
  s=Regex.Replace(s,@"[._]+"," ");
  s=Regex.Replace(s,@"\s+-\s+[A-Za-z0-9][A-Za-z0-9._-]{1,24}\s*$"," ");
  s=Regex.Replace(s,@"[^\p{L}\p{N}'&:+-]+"," ");
  s=Regex.Replace(s,@"\s+"," ").Trim(' ','-','_','.');
  return s;
 }

 public static IEnumerable<Video> Order(IEnumerable<Video> videos){
  return (videos??Enumerable.Empty<Video>())
   .OrderBy(v=>v.MediaInfo!=null&&v.MediaInfo.IsEpisode?0:1)
   .ThenBy(v=>v.MediaInfo!=null&&v.MediaInfo.Season>=0?v.MediaInfo.Season:int.MaxValue)
   .ThenBy(v=>v.MediaInfo!=null&&v.MediaInfo.IsEpisode?v.MediaInfo.Episode:int.MaxValue)
   .ThenBy(v=>v.Name,StringComparer.OrdinalIgnoreCase);
 }

 public static string Summary(IReadOnlyCollection<Video> videos){
  int total=videos==null?0:videos.Count;
  int episodes=videos==null?0:videos.Count(v=>v.MediaInfo!=null&&v.MediaInfo.IsEpisode);
  int seasons=videos==null?0:videos.Where(v=>v.MediaInfo!=null&&v.MediaInfo.IsEpisode&&v.MediaInfo.Season>=0).Select(v=>v.MediaInfo.Season).Distinct().Count();
  string text=total+" local video"+(total==1?"":"s");
  if(seasons>0)text+=" • "+seasons+" season"+(seasons==1?"":"s");
  if(episodes>0)text+=" • "+episodes+" recognised episode"+(episodes==1?"":"s");
  return text;
 }

 public static void SelfTest(){
  var tv=Analyze("Example.Show.S02E07.The.Return.1080p.WEB-DL.mkv","Example Show","Season 02");
  if(!tv.IsEpisode||tv.Season!=2||tv.Episode!=7||tv.EpisodeCode!="S02E07")throw new InvalidOperationException("Library season/episode recognition failed.");
  var anime=Analyze("[Group] Example Series - 82 (1080p).mkv","Example Series","");
  if(!anime.IsEpisode||anime.Episode!=82||anime.Season!=-1)throw new InvalidOperationException("Library absolute episode recognition failed.");
  var movie=Analyze("Example.Movie.2026.1080p.mkv","Example Movie","");
  if(movie.IsEpisode)throw new InvalidOperationException("Library intelligence misclassified a normal movie filename as an episode.");
 }
}
}
