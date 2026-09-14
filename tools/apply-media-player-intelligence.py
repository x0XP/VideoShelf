from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, text):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")


def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


player_path = "src/VideoShelf.TransferHost/Player/OptimizedStreamForm.cs"
player = read(player_path)
player = replace_once(player, "internal sealed class OptimizedStreamForm : Form", "internal sealed partial class OptimizedStreamForm : Form", "partial player")
player = replace_once(
    player,
    '''        subtitleChoice.SetBounds(216, 11, 180, 31); subtitleChoice.Enabled = false;\n        mute.SetBounds(410, 9, 64, 34);\n        volume.SetBounds(484, 11, 110, 30);\n        volumeLabel.SetBounds(600, 15, 48, 22); volumeLabel.TextAlign = ContentAlignment.MiddleLeft; volumeLabel.ForeColor = Color.FromArgb(190, 205, 220);''',
    '''        audioChoice.SetBounds(216, 11, 160, 31); audioChoice.Enabled = false;\n        subtitleChoice.SetBounds(384, 11, 170, 31); subtitleChoice.Enabled = false;\n        mute.SetBounds(562, 9, 64, 34);\n        volume.SetBounds(636, 11, 100, 30);\n        volumeLabel.SetBounds(742, 15, 48, 22); volumeLabel.TextAlign = ContentAlignment.MiddleLeft; volumeLabel.ForeColor = Color.FromArgb(190, 205, 220);''',
    "transport controls")
player = replace_once(
    player,
    "bottom.Controls.AddRange(new Control[] { playPause, stop, subtitleChoice, mute, volume, volumeLabel, seek, timeLabel });",
    "bottom.Controls.AddRange(new Control[] { playPause, stop, audioChoice, subtitleChoice, mute, volume, volumeLabel, seek, timeLabel });",
    "transport add range")
player = replace_once(
    player,
    "        subtitleChoice.SelectedIndexChanged += (_, _) => ApplySubtitleSelection();",
    "        audioChoice.SelectedIndexChanged += (_, _) => ApplyAudioSelection();\n        subtitleChoice.SelectedIndexChanged += (_, _) => ApplySubtitleSelection();",
    "audio event")
player = replace_once(
    player,
    "        fileChoice.Enabled = true;\n        subtitleOptions.Add(new SubtitleOption(SubtitleOption.AutoId, \"Subtitles: Auto (English)\"));",
    "        fileChoice.Enabled = true;\n        ConfigureFixtureAudio();\n        subtitleOptions.Add(new SubtitleOption(SubtitleOption.AutoId, \"Subtitles: Auto (English)\"));",
    "fixture audio")
player = replace_once(
    player,
    '''        int subtitleWidth = ClientSize.Width < 900 ? 150 : 180;\n        subtitleChoice.SetBounds(216, 11, subtitleWidth, 31);\n        mute.SetBounds(subtitleChoice.Right + 10, 9, 64, 34);\n        int volumeWidth = ClientSize.Width < 900 ? 88 : 110;\n        volume.SetBounds(mute.Right + 10, 11, volumeWidth, 30);\n        volumeLabel.SetBounds(volume.Right + 6, 15, 48, 22);''',
    '''        bool compact = ClientSize.Width < 900;\n        int audioWidth = compact ? 120 : 160;\n        int subtitleWidth = compact ? 130 : 170;\n        audioChoice.SetBounds(216, 11, audioWidth, 31);\n        subtitleChoice.SetBounds(audioChoice.Right + 8, 11, subtitleWidth, 31);\n        mute.SetBounds(subtitleChoice.Right + 8, 9, 64, 34);\n        int volumeWidth = compact ? 76 : 100;\n        volume.SetBounds(mute.Right + 8, 11, volumeWidth, 30);\n        volumeLabel.SetBounds(volume.Right + 6, 15, 44, 22);''',
    "adaptive layout")
player = replace_once(
    player,
    '            var streamSession = new StreamingTorrentSession(discoveryCache, Path.Combine(cache, "metadata"));',
    '            var streamSession = await CreateStreamingSessionAsync(discoveryCache, Path.Combine(cache, "metadata"), cts.Token);',
    "primary session async")
player = replace_once(
    player,
    '                streamSession = new StreamingTorrentSession(discoveryCache, Path.Combine(cache, "metadata-fallback"));',
    '                streamSession = await CreateStreamingSessionAsync(discoveryCache, Path.Combine(cache, "metadata-fallback"), cts.Token);',
    "fallback session async")
player = replace_once(
    player,
    '''            Core.Initialize();\n            var localVlc = new LibVLC("--no-video-title-show", "--network-caching=5000", "--file-caching=5000", "--clock-jitter=0", "--clock-synchro=0");''',
    '''            status.Text = "Preparing playback engine…";\n            var localVlc = await CreateLibVlcAsync(cts.Token);''',
    "libvlc async")
player = replace_once(
    player,
    '''    async Task PrebufferAsync(ITorrentManagerFile file, int request)\n    {\n        TorrentManager? activeManager = manager;\n        if (activeManager == null) return;\n        var streamProvider = activeManager.StreamProvider ?? throw new InvalidOperationException("Torrent streaming provider is unavailable.");\n        long target = CalculatePrebufferBytes(file.Length), readTotal = 0;\n        byte[] buffer = new byte[256 * 1024];\n        videoOverlay.Visible = true; video.Visible = false;\n        using Stream warm = await streamProvider.CreateStreamAsync(file, false, cts.Token);\n        var clock = Stopwatch.StartNew();\n        while (readTotal < target)\n        {\n            if (request != playbackRevision || closing) throw new OperationCanceledException();\n            int want = (int)Math.Min(buffer.Length, target - readTotal);\n            int read = await warm.ReadAsync(buffer.AsMemory(0, want), cts.Token);\n            if (read <= 0) break;\n            readTotal += read;\n            double mb = readTotal / (1024d * 1024d), totalMb = target / (1024d * 1024d);\n            double measured = clock.Elapsed.TotalSeconds > 0.5 ? mb / clock.Elapsed.TotalSeconds : 0;\n            double swarm = activeManager.Monitor.DownloadRate / (1024d * 1024d);\n            double rate = Math.Max(measured, swarm);\n            status.Text = $"Buffering {mb:0.0}/{totalMb:0.0} MB • {rate:0.0} MB/s";\n            videoOverlay.Text = $"Buffering selected video…\\n\\n{mb:0.0} / {totalMb:0.0} MB";\n        }\n    }''',
    '''    async Task PrebufferAsync(ITorrentManagerFile file, int request)\n    {\n        TorrentManager? activeManager = manager;\n        if (activeManager == null) return;\n        var streamProvider = activeManager.StreamProvider ?? throw new InvalidOperationException("Torrent streaming provider is unavailable.");\n        long target = CalculatePrebufferBytes(file.Length), readTotal = 0;\n        byte[] buffer = new byte[256 * 1024];\n        videoOverlay.Visible = true; video.Visible = false;\n        using Stream warm = await streamProvider.CreateStreamAsync(file, false, cts.Token);\n        var clock = Stopwatch.StartNew();\n        while (readTotal < target)\n        {\n            if (request != playbackRevision || closing) throw new OperationCanceledException();\n            int want = (int)Math.Min(buffer.Length, Math.Max(1, target - readTotal));\n            int read = await warm.ReadAsync(buffer.AsMemory(0, want), cts.Token);\n            if (read <= 0) break;\n            readTotal += read;\n            double measuredBytes = clock.Elapsed.TotalSeconds > 0.5 ? readTotal / clock.Elapsed.TotalSeconds : 0;\n            double swarmBytes = activeManager.Monitor.DownloadRate;\n            double rateBytes = Math.Max(measuredBytes, swarmBytes);\n            if (readTotal >= 1024L * 1024L && rateBytes > 0)\n            {\n                target = CalculateAdaptivePrebufferBytes(file.Length, rateBytes, readTotal);\n                adaptiveNetworkCacheMs = CalculateAdaptiveNetworkCacheMs(rateBytes);\n                adaptiveRateBytesPerSecond = rateBytes;\n                adaptivePrebufferBytes = target;\n            }\n            double mb = readTotal / (1024d * 1024d), totalMb = target / (1024d * 1024d);\n            double rate = rateBytes / (1024d * 1024d);\n            status.Text = $"Buffering {mb:0.0}/{totalMb:0.0} MB • {rate:0.0} MB/s • adaptive";\n            videoOverlay.Text = $"Buffering selected video…\\n\\n{mb:0.0} / {totalMb:0.0} MB";\n        }\n        adaptivePrebufferBytes = Math.Max(adaptivePrebufferBytes, readTotal);\n    }''',
    "adaptive prebuffer")
player = replace_once(
    player,
    "                ResetSubtitleChoices();",
    "                ResetAudioChoices();\n                ResetSubtitleChoices();",
    "reset audio")
player = replace_once(
    player,
    '''                currentMedia = new Media(activeVlc, new Uri(activeSession.StreamingUrl(httpStream.RelativeUri)));\n                bool started = activePlayer.Play(currentMedia);''',
    '''                currentMedia = new Media(activeVlc, new Uri(activeSession.StreamingUrl(httpStream.RelativeUri)));\n                currentMedia.AddOption(":network-caching=" + adaptiveNetworkCacheMs.ToString(System.Globalization.CultureInfo.InvariantCulture));\n                currentMedia.AddOption(":file-caching=" + adaptiveNetworkCacheMs.ToString(System.Globalization.CultureInfo.InvariantCulture));\n                bool started = activePlayer.Play(currentMedia);''',
    "adaptive media options")
player = replace_once(
    player,
    '''                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;\n                RefreshSubtitleChoices();''',
    '''                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;\n                RefreshAudioChoices();\n                RefreshSubtitleChoices();''',
    "refresh audio")
player = replace_once(
    player,
    '''    void RefreshStats()\n    {\n        RefreshSubtitleChoices();\n        if (manager != null && selected != null && currentMedia != null)\n            status.Text = $"{manager.State} • {manager.Monitor.DownloadRate / (1024d * 1024d):0.0} MB/s • buffered local stream";''',
    '''    void RefreshStats()\n    {\n        RefreshAudioChoices();\n        RefreshSubtitleChoices();\n        if (manager != null && selected != null && currentMedia != null)\n            status.Text = $"{manager.State} • {manager.Monitor.DownloadRate / (1024d * 1024d):0.0} MB/s • adaptive {adaptiveNetworkCacheMs / 1000d:0.0}s cache";''',
    "stats audio adaptive")
player = replace_once(
    player,
    '''            if (OptimizedStreamForm.CalculatePrebufferBytes(1024L * 1024 * 1024) < 8L * 1024 * 1024)\n                throw new InvalidOperationException("Streaming prebuffer target is too small.");''',
    '''            if (OptimizedStreamForm.CalculatePrebufferBytes(1024L * 1024 * 1024) < 8L * 1024 * 1024)\n                throw new InvalidOperationException("Streaming prebuffer target is too small.");\n            OptimizedStreamForm.AdaptiveStreamingSelfTest();\n            OptimizedStreamForm.AudioSelectionSelfTest();''',
    "player intelligence self tests")
write(player_path, player)


partial = r'''using System.Globalization;
using LibVLCSharp.Shared;

namespace VideoShelf.TransferHost;

internal sealed partial class OptimizedStreamForm
{
    readonly DarkComboBox audioChoice = new() { Width = 160, EmptyText = "Audio" };
    readonly List<AudioOption> audioOptions = new();
    string audioSignature = "";
    bool suppressAudioChange;
    int audioSelection = AudioOption.AutoId;
    int adaptiveNetworkCacheMs = 5000;
    long adaptivePrebufferBytes;
    double adaptiveRateBytesPerSecond;

    sealed class AudioOption
    {
        public const int AutoId = int.MinValue;
        public int Id { get; }
        public string Name { get; }
        public AudioOption(int id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }

    void ConfigureFixtureAudio()
    {
        audioOptions.Add(new AudioOption(AudioOption.AutoId, "Audio: Auto (English)"));
        audioOptions.Add(new AudioOption(1, "English • Stereo"));
        audioOptions.Add(new AudioOption(2, "Japanese • Stereo"));
        foreach (var option in audioOptions) audioChoice.Items.Add(option);
        audioChoice.SelectedIndex = 0;
        audioChoice.Enabled = true;
    }

    void ResetAudioChoices()
    {
        suppressAudioChange = true;
        audioChoice.Items.Clear();
        audioOptions.Clear();
        audioChoice.Enabled = false;
        audioChoice.EmptyText = "Audio";
        audioSignature = "";
        audioSelection = AudioOption.AutoId;
        suppressAudioChange = false;
    }

    void RefreshAudioChoices()
    {
        MediaPlayer? activePlayer = player;
        if (activePlayer == null || currentMedia == null) return;

        LibVLCSharp.Shared.Structures.TrackDescription[] descriptions;
        try { descriptions = activePlayer.AudioTrackDescription; }
        catch { return; }

        var tracks = descriptions
            .Where(d => d.Id >= 0)
            .Select(d => new AudioOption(d.Id, CleanAudioName(d.Name, d.Id)))
            .ToList();
        string nextSignature = string.Join("\u001f", tracks.Select(t => t.Id.ToString(CultureInfo.InvariantCulture) + ":" + t.Name));
        if (nextSignature == audioSignature) return;
        audioSignature = nextSignature;

        suppressAudioChange = true;
        audioChoice.Items.Clear();
        audioOptions.Clear();
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string languageName = culture.EnglishName.Split('(')[0].Trim();
        string autoLabel = culture.TwoLetterISOLanguageName.Equals("iv", StringComparison.OrdinalIgnoreCase)
            ? "Audio: Auto"
            : "Audio: Auto (" + languageName + ")";
        audioOptions.Add(new AudioOption(AudioOption.AutoId, autoLabel));
        foreach (var track in tracks) audioOptions.Add(track);
        foreach (var option in audioOptions) audioChoice.Items.Add(option);

        int selectedIndex;
        if (audioSelection == AudioOption.AutoId) selectedIndex = 0;
        else
        {
            selectedIndex = audioOptions.FindIndex(x => x.Id == audioSelection);
            if (selectedIndex < 0)
            {
                audioSelection = AudioOption.AutoId;
                selectedIndex = 0;
            }
        }
        audioChoice.SelectedIndex = selectedIndex;
        audioChoice.Enabled = tracks.Count > 0;
        audioChoice.EmptyText = tracks.Count > 0 ? autoLabel : "No audio tracks";
        suppressAudioChange = false;

        if (audioSelection == AudioOption.AutoId) ApplyAutoAudio(activePlayer, tracks);
        else activePlayer.SetAudioTrack(audioSelection);
    }

    static string CleanAudioName(string? name, int id)
    {
        string value = (name ?? "").Trim();
        if (value.Length == 0 || value.Equals("Track", StringComparison.OrdinalIgnoreCase)) value = "Audio " + id;
        return value.Replace("\t", " ");
    }

    static void ApplyAutoAudio(MediaPlayer activePlayer, IReadOnlyList<AudioOption> tracks)
    {
        if (tracks.Count == 0) return;
        int preferred = ChoosePreferredAudioId(tracks, CultureInfo.CurrentUICulture);
        if (preferred < 0 && tracks.Any(t => t.Id == activePlayer.AudioTrack)) preferred = activePlayer.AudioTrack;
        if (preferred < 0) preferred = tracks[0].Id;
        activePlayer.SetAudioTrack(preferred);
    }

    static int ChoosePreferredAudioId(IReadOnlyList<AudioOption> tracks, CultureInfo culture)
    {
        if (tracks.Count == 0) return -1;
        string two = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        string three;
        try { three = culture.ThreeLetterISOLanguageName.ToLowerInvariant(); } catch { three = ""; }
        string englishName = culture.EnglishName.Split('(')[0].Trim().ToLowerInvariant();
        string nativeName = culture.NativeName.Split('(')[0].Trim().ToLowerInvariant();
        int bestId = -1, bestScore = int.MinValue;
        foreach (var track in tracks)
        {
            string label = track.Name.ToLowerInvariant();
            var tokens = System.Text.RegularExpressions.Regex.Split(label, @"[^\p{L}\p{N}]+")
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            int score = 0;
            if (two.Length > 1 && tokens.Contains(two)) score += 8;
            if (three.Length > 2 && tokens.Contains(three)) score += 9;
            if (englishName.Length > 2 && label.Contains(englishName, StringComparison.OrdinalIgnoreCase)) score += 10;
            if (nativeName.Length > 2 && label.Contains(nativeName, StringComparison.OrdinalIgnoreCase)) score += 9;
            if (label.Contains("main", StringComparison.OrdinalIgnoreCase) || label.Contains("default", StringComparison.OrdinalIgnoreCase)) score += 2;
            if (label.Contains("commentary", StringComparison.OrdinalIgnoreCase)) score -= 12;
            if (label.Contains("description", StringComparison.OrdinalIgnoreCase) || label.Contains("descriptive", StringComparison.OrdinalIgnoreCase)) score -= 6;
            if (score > bestScore && score > 0) { bestScore = score; bestId = track.Id; }
        }
        return bestId;
    }

    void ApplyAudioSelection()
    {
        if (suppressAudioChange || player == null) return;
        int index = audioChoice.SelectedIndex;
        if (index < 0 || index >= audioOptions.Count) return;
        AudioOption option = audioOptions[index];
        audioSelection = option.Id;
        if (option.Id == AudioOption.AutoId)
            ApplyAutoAudio(player, audioOptions.Where(t => t.Id >= 0).ToList());
        else
            player.SetAudioTrack(option.Id);
    }

    static async Task<StreamingTorrentSession> CreateStreamingSessionAsync(string stateRoot, string temporaryRoot, CancellationToken token)
    {
        Task<StreamingTorrentSession> startup = Task.Run(() => new StreamingTorrentSession(stateRoot, temporaryRoot), token);
        Task delay = Task.Delay(TimeSpan.FromSeconds(15), token);
        Task completed = await Task.WhenAny(startup, delay);
        if (!ReferenceEquals(completed, startup))
        {
            token.ThrowIfCancellationRequested();
            _ = startup.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion) try { t.Result.Dispose(); } catch { }
            }, TaskScheduler.Default);
            throw new TimeoutException("The torrent engine took too long to initialise. Please retry the stream.");
        }
        return await startup;
    }

    static async Task<LibVLC> CreateLibVlcAsync(CancellationToken token)
    {
        Task<LibVLC> startup = Task.Run(() =>
        {
            Core.Initialize();
            return new LibVLC("--no-video-title-show", "--network-caching=5000", "--file-caching=5000", "--clock-jitter=0", "--clock-synchro=0");
        }, token);
        Task delay = Task.Delay(TimeSpan.FromSeconds(12), token);
        Task completed = await Task.WhenAny(startup, delay);
        if (!ReferenceEquals(completed, startup))
        {
            token.ThrowIfCancellationRequested();
            _ = startup.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion) try { t.Result.Dispose(); } catch { }
            }, TaskScheduler.Default);
            throw new TimeoutException("The playback engine took too long to initialise. Please retry the stream.");
        }
        return await startup;
    }

    internal static long CalculateAdaptivePrebufferBytes(long fileLength, double bytesPerSecond, long alreadyBuffered)
    {
        const long mb = 1024L * 1024L;
        long target;
        double rate = bytesPerSecond / mb;
        if (rate >= 12) target = 6 * mb;
        else if (rate >= 6) target = 8 * mb;
        else if (rate >= 3) target = 10 * mb;
        else if (rate >= 1.5) target = 12 * mb;
        else if (rate > 0) target = 14 * mb;
        else target = CalculatePrebufferBytes(fileLength);
        target = Math.Max(target, alreadyBuffered);
        if (fileLength > 0) target = Math.Min(fileLength, target);
        return Math.Max(1, target);
    }

    internal static int CalculateAdaptiveNetworkCacheMs(double bytesPerSecond)
    {
        const double mb = 1024d * 1024d;
        double rate = bytesPerSecond / mb;
        if (rate >= 12) return 1500;
        if (rate >= 6) return 2500;
        if (rate >= 3) return 4000;
        if (rate >= 1.5) return 6000;
        if (rate > 0) return 8000;
        return 5000;
    }

    internal static void AdaptiveStreamingSelfTest()
    {
        const long gb = 1024L * 1024L * 1024L;
        long fast = CalculateAdaptivePrebufferBytes(gb, 16d * 1024 * 1024, 1024 * 1024);
        long slow = CalculateAdaptivePrebufferBytes(gb, 1d * 1024 * 1024, 1024 * 1024);
        if (fast >= slow) throw new InvalidOperationException("Adaptive prebuffering no longer starts faster on strong swarms.");
        if (CalculateAdaptiveNetworkCacheMs(16d * 1024 * 1024) >= CalculateAdaptiveNetworkCacheMs(1d * 1024 * 1024))
            throw new InvalidOperationException("Adaptive network caching no longer expands on slower swarms.");
        if (CalculateAdaptivePrebufferBytes(4L * 1024 * 1024, 20d * 1024 * 1024, 0) > 4L * 1024 * 1024)
            throw new InvalidOperationException("Adaptive prebuffering exceeded the selected file length.");
    }

    internal static void AudioSelectionSelfTest()
    {
        var tracks = new List<AudioOption>
        {
            new AudioOption(1, "Japanese Stereo"),
            new AudioOption(2, "English Stereo"),
            new AudioOption(3, "English Commentary")
        };
        if (ChoosePreferredAudioId(tracks, CultureInfo.GetCultureInfo("en-GB")) != 2)
            throw new InvalidOperationException("Language-aware audio selection did not prefer the normal matching language track.");
    }
}
'''
write("src/VideoShelf.TransferHost/Player/OptimizedStreamForm.Intelligence.cs", partial)


library_helper = r'''using System;
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
'''
write("src/VideoShelf/Media/LibraryMediaIntelligence.cs", library_helper)


app = read("src/VideoShelf/Application/VideoShelf.cs")
app = replace_once(
    app,
    "FolderNaming.SelfTest();BuiltInOnlineSearch.SelfTest();",
    "FolderNaming.SelfTest();LibraryMediaIntelligence.SelfTest();BuiltInOnlineSearch.SelfTest();",
    "library self test")
app = replace_once(
    app,
    "sealed class Video { public string Path,Name,Relative; public long Size; public DateTime Modified; }",
    "sealed class Video { public string Path,Name,Relative; public long Size; public DateTime Modified; public LibraryMediaInfo MediaInfo=new LibraryMediaInfo(); }",
    "video intelligence model")
write("src/VideoShelf/Application/VideoShelf.cs", app)


project = read("VideoShelf.csproj")
project = replace_once(
    project,
    '    <Compile Include="src\\VideoShelf\\Media\\TransferBridge.cs" />',
    '    <Compile Include="src\\VideoShelf\\Media\\TransferBridge.cs" />\n    <Compile Include="src\\VideoShelf\\Media\\LibraryMediaIntelligence.cs" />',
    "project library include")
write("VideoShelf.csproj", project)


library = read("src/VideoShelf/UI/Shelf/Shelf.Library.cs")
library = replace_once(
    library,
    '''try{var info=new FileInfo(f);list.Add(new Video{Path=f,Name=info.Name,Size=info.Length,Modified=info.LastWriteTime,Relative=dir.Length==p.Path.Length?"—":dir.Substring(p.Path.Length).TrimStart(Path.DirectorySeparatorChar)});}catch{errors++;}''',
    '''try{var info=new FileInfo(f);string relative=dir.Length==p.Path.Length?"—":dir.Substring(p.Path.Length).TrimStart(Path.DirectorySeparatorChar);list.Add(new Video{Path=f,Name=info.Name,Size=info.Length,Modified=info.LastWriteTime,Relative=relative,MediaInfo=LibraryMediaIntelligence.Analyze(info.Name,p.Name,relative)});}catch{errors++;}''',
    "scan intelligence")
library = replace_once(
    library,
    ''' void RenderLocal(){\n  files.BeginUpdate();files.Items.Clear();foreach(var v in videos.OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase)){var item=new ListViewItem(new[]{v.Name,Path.GetExtension(v.Path).TrimStart('.').ToUpperInvariant(),SizeText(v.Size),v.Modified.ToString("dd MMM yyyy HH:mm"),v.Relative});item.Tag=v;files.Items.Add(item);}files.EndUpdate();SetStatus(files.Items.Count+" local video"+(files.Items.Count==1?"":"s"),files.Items.Count==0?"No local videos found. Use Find online to search metadata.":"Double-click a video to play it.");RefreshCollectionVisualState();\n }''',
    ''' void RenderLocal(){\n  files.BeginUpdate();files.Items.Clear();files.Groups.Clear();var ordered=LibraryMediaIntelligence.Order(videos).ToList();bool grouped=ordered.Any(v=>v.MediaInfo!=null&&v.MediaInfo.IsEpisode);files.ShowGroups=grouped;var groups=new Dictionary<string,ListViewGroup>(StringComparer.OrdinalIgnoreCase);foreach(var v in ordered){var media=v.MediaInfo??new LibraryMediaInfo();var item=new ListViewItem(new[]{v.Name,media.IsEpisode?media.EpisodeCode:"—",Path.GetExtension(v.Path).TrimStart('.').ToUpperInvariant(),SizeText(v.Size),v.Modified.ToString("dd MMM yyyy HH:mm"),v.Relative});item.Tag=v;item.ToolTipText=media.IsEpisode?(media.EpisodeCode+(media.EpisodeTitle.Length>0?" • "+media.EpisodeTitle:"")+"\n"+v.Name):v.Name;if(grouped){string label=media.IsEpisode?media.GroupLabel:"Other videos";ListViewGroup group;if(!groups.TryGetValue(label,out group)){group=new ListViewGroup(label,HorizontalAlignment.Left);groups[label]=group;files.Groups.Add(group);}item.Group=group;}files.Items.Add(item);}files.EndUpdate();string summary=LibraryMediaIntelligence.Summary(videos);SetStatus(summary,files.Items.Count==0?"No local videos found. Use Find online to search metadata.":grouped?"Episodes are grouped by season where VideoShelf can recognise them.":"Double-click a video to play it.");RefreshCollectionVisualState();\n }''',
    "render intelligence")
write("src/VideoShelf/UI/Shelf/Shelf.Library.cs", library)


core = read("src/VideoShelf/UI/Shelf/Shelf.Core.cs")
core = replace_once(
    core,
    'files.View=View.Details;files.FullRowSelect=true;files.MultiSelect=false;files.HideSelection=false;files.BackColor=Color.FromArgb(8,17,25);files.ForeColor=XdolfTheme.Text;files.BorderStyle=BorderStyle.FixedSingle;files.Columns.Add("VIDEO",500);files.Columns.Add("TYPE",80);files.Columns.Add("SIZE",110);files.Columns.Add("MODIFIED",160);files.Columns.Add("SUBFOLDER",220);',
    'files.View=View.Details;files.FullRowSelect=true;files.MultiSelect=false;files.HideSelection=false;files.ShowItemToolTips=true;files.BackColor=Color.FromArgb(8,17,25);files.ForeColor=XdolfTheme.Text;files.BorderStyle=BorderStyle.FixedSingle;files.Columns.Add("VIDEO",420);files.Columns.Add("EPISODE",95);files.Columns.Add("TYPE",80);files.Columns.Add("SIZE",110);files.Columns.Add("MODIFIED",160);files.Columns.Add("SUBFOLDER",220);',
    "episode column")
write("src/VideoShelf/UI/Shelf/Shelf.Core.cs", core)

print("Applied audio selection, asynchronous startup, adaptive streaming, and library intelligence.")
