using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoShelf {
sealed class UpdateInfo {
 public string VersionText="";
 public string Notes="";
 public string ReleaseUrl="";
 public string InstallerName="";
 public string InstallerUrl="";
 public string Sha256="";
 public long Size;
}

static class UpdateService {
 const string LatestReleaseApi="https://api.github.com/repos/x0XP/VideoShelf/releases/latest";
 const long MaxInstallerBytes=512L*1024L*1024L;
 static readonly Regex Sha256Regex=new Regex("^[0-9a-f]{64}$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
 static readonly HttpClient http=CreateHttp();

 sealed class ReleaseDto {
  public string tag_name { get; set; }
  public string html_url { get; set; }
  public string body { get; set; }
  public bool draft { get; set; }
  public bool prerelease { get; set; }
  public List<AssetDto> assets { get; set; }
 }
 sealed class AssetDto {
  public string name { get; set; }
  public string browser_download_url { get; set; }
  public string digest { get; set; }
  public string state { get; set; }
  public long size { get; set; }
 }

 static HttpClient CreateHttp(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var handler=new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
  var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(25)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd(AppVersion.UserAgent);
  client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
  client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2026-03-10");
  return client;
 }

 public static async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct){
  using(var request=new HttpRequestMessage(HttpMethod.Get,LatestReleaseApi))
  using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseContentRead,ct).ConfigureAwait(false)){
   if(response.StatusCode==HttpStatusCode.NotFound)return null;
   response.EnsureSuccessStatusCode();
   string json=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
   ct.ThrowIfCancellationRequested();
   var release=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<ReleaseDto>(json);
   return BuildUpdateInfo(release);
  }
 }

 static UpdateInfo BuildUpdateInfo(ReleaseDto release){
  if(release==null||release.draft||release.prerelease||string.IsNullOrWhiteSpace(release.tag_name))return null;
  Version releaseVersion;
  try{releaseVersion=AppVersion.Parse(release.tag_name);}catch{return null;}
  if(releaseVersion<=AppVersion.Parsed)return null;
  string versionText=AppVersion.Normalize(release.tag_name);
  AssetDto installer=(release.assets??new List<AssetDto>())
   .Where(IsUsableInstaller)
   .FirstOrDefault(a=>InstallerMatchesVersion(a.name,releaseVersion));
  if(installer==null)return null;
  string digest=NormalizeDigest(installer.digest);
  if(digest.Length==0)return null;
  if(installer.size<=0||installer.size>MaxInstallerBytes)return null;
  Uri downloadUri;
  if(!Uri.TryCreate(installer.browser_download_url,UriKind.Absolute,out downloadUri)||downloadUri.Scheme!=Uri.UriSchemeHttps)return null;
  return new UpdateInfo{
   VersionText=versionText,
   Notes=(release.body??"").Trim(),
   ReleaseUrl=release.html_url??"",
   InstallerName=installer.name,
   InstallerUrl=installer.browser_download_url,
   Sha256=digest,
   Size=installer.size
  };
 }

 static bool IsUsableInstaller(AssetDto asset){
  return asset!=null&&string.Equals(asset.state,"uploaded",StringComparison.OrdinalIgnoreCase)&&
   !string.IsNullOrWhiteSpace(asset.name)&&asset.name.StartsWith("VideoShelf-Setup-v",StringComparison.OrdinalIgnoreCase)&&asset.name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);
 }
 static bool InstallerMatchesVersion(string name,Version releaseVersion){
  if(string.IsNullOrWhiteSpace(name))return false;
  const string prefix="VideoShelf-Setup-v";const string suffix=".exe";
  if(!name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return false;
  string raw=name.Substring(prefix.Length,name.Length-prefix.Length-suffix.Length);
  try{return AppVersion.Parse(raw)==releaseVersion;}catch{return false;}
 }
 static string NormalizeDigest(string digest){
  string value=(digest??"").Trim();
  if(value.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase))value=value.Substring(7);
  return Sha256Regex.IsMatch(value)?value.ToLowerInvariant():"";
 }

 public static async Task<string> DownloadInstallerAsync(UpdateInfo update,IProgress<int> progress,CancellationToken ct){
  if(update==null)throw new ArgumentNullException("update");
  if(NormalizeDigest(update.Sha256).Length==0)throw new InvalidOperationException("The release does not provide a valid SHA-256 digest.");
  if(update.Size<=0||update.Size>MaxInstallerBytes)throw new InvalidOperationException("The installer size is invalid.");
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VideoShelf","Updates",update.VersionText);
  Directory.CreateDirectory(folder);
  string finalPath=Path.Combine(folder,Path.GetFileName(update.InstallerName));
  string partialPath=finalPath+".download";
  try{
   if(File.Exists(finalPath)&&VerifySha256(finalPath,update.Sha256)){if(progress!=null)progress.Report(100);return finalPath;}
   if(File.Exists(partialPath))File.Delete(partialPath);
   using(var response=await http.GetAsync(update.InstallerUrl,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
    response.EnsureSuccessStatusCode();
    long declared=response.Content.Headers.ContentLength??update.Size;
    if(declared<=0||declared>MaxInstallerBytes)throw new InvalidOperationException("The update download is unexpectedly large.");
    using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
    using(var output=new FileStream(partialPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)){
     var buffer=new byte[81920];long total=0;int last=-1;
     while(true){
      int read=await input.ReadAsync(buffer,0,buffer.Length,ct).ConfigureAwait(false);if(read<=0)break;
      total+=read;if(total>MaxInstallerBytes)throw new InvalidOperationException("The update download exceeded the maximum allowed size.");
      await output.WriteAsync(buffer,0,read,ct).ConfigureAwait(false);
      int pct=(int)Math.Min(99,total*100L/Math.Max(1,declared));if(progress!=null&&pct!=last){last=pct;progress.Report(pct);}
     }
    }
   }
   if(!VerifySha256(partialPath,update.Sha256))throw new InvalidOperationException("The downloaded installer failed SHA-256 verification and was not launched.");
   if(File.Exists(finalPath))File.Delete(finalPath);
   File.Move(partialPath,finalPath);
   if(progress!=null)progress.Report(100);
   return finalPath;
  }catch{try{if(File.Exists(partialPath))File.Delete(partialPath);}catch{}throw;}
 }

 static bool VerifySha256(string path,string expected){
  string normalized=NormalizeDigest(expected);if(normalized.Length==0||!File.Exists(path))return false;
  using(var sha=SHA256.Create())using(var stream=File.OpenRead(path)){
   string actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
   return string.Equals(actual,normalized,StringComparison.OrdinalIgnoreCase);
  }
 }

 public static void LaunchInstallerAndRestart(string installerPath){
  if(string.IsNullOrWhiteSpace(installerPath)||!File.Exists(installerPath))throw new FileNotFoundException("The verified update installer could not be found.",installerPath);
  string currentDir=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  string defaultDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","VideoShelf");
  string restartDir=File.Exists(Path.Combine(currentDir,"unins000.exe"))?currentDir:defaultDir;
  string restartExe=Path.Combine(restartDir,"VideoShelf.exe");
  string helper=Path.Combine(Path.GetDirectoryName(installerPath),"apply-update-"+Guid.NewGuid().ToString("N")+".cmd");
  var script=new StringBuilder();
  script.AppendLine("@echo off");
  script.AppendLine("ping 127.0.0.1 -n 3 >nul");
  script.AppendLine("start /wait \"\" \""+EscapeBatch(installerPath)+"\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS");
  script.AppendLine("if errorlevel 1 exit /b %errorlevel%");
  script.AppendLine("if exist \""+EscapeBatch(restartExe)+"\" start \"\" \""+EscapeBatch(restartExe)+"\"");
  script.AppendLine("del \"%~f0\"");
  File.WriteAllText(helper,script.ToString(),Encoding.ASCII);
  var psi=new ProcessStartInfo("cmd.exe","/d /c \"\""+helper+"\"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(helper)};
  Process.Start(psi);
 }
 static string EscapeBatch(string value){return (value??"").Replace("%","%%").Replace("\"","\"\"");}

 public static void SelfTest(){
  if(NormalizeDigest("sha256:"+new string('a',64))!=new string('a',64))throw new InvalidOperationException("Update digest self-test failed.");
  if(NormalizeDigest("md5:"+new string('a',32)).Length!=0)throw new InvalidOperationException("Update digest validation self-test failed.");
  var release=new ReleaseDto{tag_name="v1.8.0",html_url="https://github.com/x0XP/VideoShelf/releases/tag/v1.8.0",body="test",assets=new List<AssetDto>{new AssetDto{name="VideoShelf-Setup-v1.8.0.exe",browser_download_url="https://github.com/x0XP/VideoShelf/releases/download/v1.8.0/VideoShelf-Setup-v1.8.0.exe",digest="sha256:"+new string('b',64),state="uploaded",size=1024}}};
  var info=BuildUpdateInfo(release);if(info==null||info.VersionText!="1.8.0"||info.Sha256!=new string('b',64))throw new InvalidOperationException("Update release parsing self-test failed.");
  release.prerelease=true;if(BuildUpdateInfo(release)!=null)throw new InvalidOperationException("Prerelease update filtering self-test failed.");
 }
}
}
