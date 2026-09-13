using System;
using System.Globalization;

namespace VideoShelf {
static class AppVersion {
 public const string Current="1.7.9";
 public static string Display { get { var v=Parse(Current);return v.Build>0?v.Major+"."+v.Minor+"."+v.Build:v.Major+"."+v.Minor; } }
 public static string UserAgent { get { return "VideoShelf/"+Current; } }
 public static Version Parsed { get { return Parse(Current); }
 }

 public static Version Parse(string value){
  string text=(value??"").Trim();
  if(text.StartsWith("v",StringComparison.OrdinalIgnoreCase))text=text.Substring(1);
  int dash=text.IndexOf('-');if(dash>=0)text=text.Substring(0,dash);
  int plus=text.IndexOf('+');if(plus>=0)text=text.Substring(0,plus);
  Version version;
  if(!Version.TryParse(text,out version))throw new FormatException("Invalid VideoShelf version: "+value);
  return version;
 }
 public static bool IsNewer(string candidate){return Parse(candidate)>Parsed;}
 public static string Normalize(string value){
  var v=Parse(value);
  if(v.Revision>=0)return string.Format(CultureInfo.InvariantCulture,"{0}.{1}.{2}.{3}",v.Major,v.Minor,Math.Max(0,v.Build),v.Revision);
  if(v.Build>=0)return string.Format(CultureInfo.InvariantCulture,"{0}.{1}.{2}",v.Major,v.Minor,v.Build);
  return string.Format(CultureInfo.InvariantCulture,"{0}.{1}",v.Major,v.Minor);
 }
 public static void SelfTest(){
  if(!IsNewer("v1.7.10")||!IsNewer("2.0.0")||IsNewer("1.7.9")||IsNewer("1.7.8"))throw new InvalidOperationException("Application version comparison self-test failed.");
  if(Normalize("v1.8.0")!="1.8.0")throw new InvalidOperationException("Application version normalization self-test failed.");
 }
}
}
