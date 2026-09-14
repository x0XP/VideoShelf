from pathlib import Path

p = Path('src/VideoShelf.TransferHost/Player/OptimizedStreamForm.cs')
s = p.read_text(encoding='utf-8')

def rep(old, new, count=1):
    global s
    found = s.count(old)
    if found != count:
        raise SystemExit(f'Expected {count} occurrence(s), found {found}: {old[:140]!r}')
    s = s.replace(old, new, count)

# Supported external subtitle payloads inside a torrent.
rep(
'''    static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".flv", ".vob" };
''',
'''    static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".flv", ".vob" };
    static readonly HashSet<string> SubtitleFiles = new(StringComparer.OrdinalIgnoreCase) { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".sup" };
''')

# Add volume UI and stable subtitle selection state.
rep(
'''    readonly Button playPause = Theme.Button("Play", 90), stop = Theme.Button("Stop", 80);
    readonly SeekBar seek = new() { Width = 280, Enabled = false };
''',
'''    readonly Button playPause = Theme.Button("Play", 90), stop = Theme.Button("Stop", 80), mute = Theme.Button("Mute", 64);
    readonly SeekBar seek = new() { Width = 280, Enabled = false };
    readonly SeekBar volume = new() { Width = 110, Enabled = true, Value = 1000 };
    readonly Label volumeLabel = Theme.Label("100%");
''')

rep(
'''    string subtitleSignature = "";
    bool suppressSubtitleChange;
    bool subtitleAutoApplied;
    int playbackRevision;
''',
'''    string subtitleSignature = "";
    bool suppressSubtitleChange;
    int subtitleSelection = SubtitleOption.AutoId;
    int currentVolume = 100;
    int lastAudibleVolume = 100;
    int playbackRevision;
''')

# Turn the transport strip into a two-row control bar so subtitles/volume don't crush the timeline.
rep(
'''        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Theme.Background };
        playPause.SetBounds(22, 16, 90, 34); playPause.Enabled = false;
        stop.SetBounds(122, 16, 80, 34);
        subtitleChoice.SetBounds(216, 18, 180, 31); subtitleChoice.Enabled = false;
        seek.SetBounds(410, 18, 460, 30); seek.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        timeLabel.SetBounds(880, 22, 150, 22); timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left; timeLabel.TextAlign = ContentAlignment.MiddleRight; timeLabel.ForeColor = Color.FromArgb(190, 205, 220);
        bottom.Controls.AddRange(new Control[] { playPause, stop, subtitleChoice, seek, timeLabel });
''',
'''        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 94, BackColor = Theme.Background };
        playPause.SetBounds(22, 9, 90, 34); playPause.Enabled = false;
        stop.SetBounds(122, 9, 80, 34);
        subtitleChoice.SetBounds(216, 11, 180, 31); subtitleChoice.Enabled = false;
        mute.SetBounds(410, 9, 64, 34);
        volume.SetBounds(484, 11, 110, 30);
        volumeLabel.SetBounds(600, 15, 48, 22); volumeLabel.TextAlign = ContentAlignment.MiddleLeft; volumeLabel.ForeColor = Color.FromArgb(190, 205, 220);
        seek.SetBounds(22, 55, 848, 30); seek.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        timeLabel.SetBounds(880, 59, 150, 22); timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left; timeLabel.TextAlign = ContentAlignment.MiddleRight; timeLabel.ForeColor = Color.FromArgb(190, 205, 220);
        bottom.Controls.AddRange(new Control[] { playPause, stop, subtitleChoice, mute, volume, volumeLabel, seek, timeLabel });
''')

rep(
'''        subtitleChoice.SelectedIndexChanged += (_, _) => ApplySubtitleSelection();
        playPause.Click += (_, _) => TogglePlayback();
        stop.Click += (_, _) => Close();
        seek.ValueCommitted += (_, _) => CommitSeek();
''',
'''        subtitleChoice.SelectedIndexChanged += (_, _) => ApplySubtitleSelection();
        playPause.Click += (_, _) => TogglePlayback();
        stop.Click += (_, _) => Close();
        mute.Click += (_, _) => ToggleMute();
        volume.ValueCommitted += (_, _) => CommitVolume();
        seek.ValueCommitted += (_, _) => CommitSeek();
''')

# Fixture mode demonstrates the controls in deterministic UI captures.
rep(
'''        subtitleChoice.SelectedIndex = 0;
        subtitleChoice.Enabled = true;
        status.Text = "Ready • 4.8 MB/s • temporary stream cache";
''',
'''        subtitleChoice.SelectedIndex = 0;
        subtitleChoice.Enabled = true;
        SetVolume(72);
        status.Text = "Ready • 4.8 MB/s • temporary stream cache";
''')

# Responsive two-row player controls.
rep(
'''    void LayoutPlayer()
    {
        status.Width = Math.Max(180, ClientSize.Width - status.Left - 22);
        int timeWidth = 150, timeX = Math.Max(620, ClientSize.Width - 22 - timeWidth);
        int subtitleWidth = ClientSize.Width < 900 ? 150 : 180;
        subtitleChoice.SetBounds(216, 18, subtitleWidth, 31);
        int seekX = subtitleChoice.Right + 14;
        timeLabel.SetBounds(timeX, 22, timeWidth, 22);
        seek.SetBounds(seekX, 18, Math.Max(120, timeX - seekX - 12), 30);
    }
''',
'''    void LayoutPlayer()
    {
        status.Width = Math.Max(180, ClientSize.Width - status.Left - 22);
        int subtitleWidth = ClientSize.Width < 900 ? 150 : 180;
        subtitleChoice.SetBounds(216, 11, subtitleWidth, 31);
        mute.SetBounds(subtitleChoice.Right + 10, 9, 64, 34);
        int volumeWidth = ClientSize.Width < 900 ? 88 : 110;
        volume.SetBounds(mute.Right + 10, 11, volumeWidth, 30);
        volumeLabel.SetBounds(volume.Right + 6, 15, 48, 22);

        int timeWidth = 150;
        int timeX = Math.Max(260, ClientSize.Width - 22 - timeWidth);
        timeLabel.SetBounds(timeX, 59, timeWidth, 22);
        seek.SetBounds(22, 55, Math.Max(140, timeX - 34), 30);
    }
''')

# Apply the remembered volume to the LibVLC player as soon as it exists.
rep(
'''            var mediaPlayer = new MediaPlayer(localVlc);
            player = mediaPlayer;
            video.MediaPlayer = mediaPlayer; uiTimer.Start();
''',
'''            var mediaPlayer = new MediaPlayer(localVlc);
            player = mediaPlayer;
            mediaPlayer.Volume = currentVolume;
            UpdateVolumeUi();
            video.MediaPlayer = mediaPlayer; uiTimer.Start();
''')

# Download only subtitle files which belong to the selected video, without blocking playback.
rep(
'''                selected = file; playPause.Enabled = false; seek.Enabled = false;
                ResetSubtitleChoices();
                try { activePlayer.Stop(); } catch { }
                currentMedia?.Dispose(); currentMedia = null; await DisposeHttpStreamAsync();
                foreach (var item in activeManager.Files) await activeManager.SetFilePriorityAsync(item, item == file ? Priority.High : Priority.DoNotDownload);
''',
'''                selected = file; playPause.Enabled = false; seek.Enabled = false;
                ResetSubtitleChoices();
                try { activePlayer.Stop(); } catch { }
                currentMedia?.Dispose(); currentMedia = null; await DisposeHttpStreamAsync();
                var externalSubtitles = FindMatchingSubtitleFiles(activeManager.Files, file, playable.Count == 1);
                foreach (var item in activeManager.Files)
                    await activeManager.SetFilePriorityAsync(item, item == file || externalSubtitles.Contains(item) ? Priority.High : Priority.DoNotDownload);
''')

rep(
'''                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;
                RefreshSubtitleChoices();
''',
'''                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;
                RefreshSubtitleChoices();
                _ = LoadExternalSubtitlesAsync(externalSubtitles, request);
''')

# Preserve explicit Auto/Off/manual choices as VLC discovers tracks over time.
rep(
'''        subtitleSignature = "";
        subtitleAutoApplied = false;
        suppressSubtitleChange = false;
''',
'''        subtitleSignature = "";
        subtitleSelection = SubtitleOption.AutoId;
        suppressSubtitleChange = false;
''')

# Correct the first pass's invalid C# regex escape.
s = s.replace('System.Text.RegularExpressions.Regex.Split(label, "[^\\p{L}\\p{N}]+")', 'System.Text.RegularExpressions.Regex.Split(label, @"[^\\p{L}\\p{N}]+")')

# Replace subtitle refresh/apply block with persistent selection + external subtitle helpers.
start = s.index('    void RefreshSubtitleChoices()\n')
end = s.index('    void TogglePlayback()\n', start)
new_block = r'''    void RefreshSubtitleChoices()
    {
        MediaPlayer? activePlayer = player;
        if (activePlayer == null || currentMedia == null) return;

        LibVLCSharp.Shared.Structures.TrackDescription[] descriptions;
        try { descriptions = activePlayer.SpuDescription; }
        catch { return; }

        var tracks = descriptions
            .Where(d => d.Id >= 0)
            .Select(d => new SubtitleOption(d.Id, CleanSubtitleName(d.Name, d.Id)))
            .ToList();

        string nextSignature = string.Join("\u001f", tracks.Select(t => t.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + t.Name));
        if (nextSignature == subtitleSignature) return;
        subtitleSignature = nextSignature;

        suppressSubtitleChange = true;
        subtitleChoice.Items.Clear();
        subtitleOptions.Clear();
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        string languageName = culture.EnglishName.Split('(')[0].Trim();
        string autoLabel = culture.TwoLetterISOLanguageName.Equals("iv", StringComparison.OrdinalIgnoreCase)
            ? "Subtitles: Auto"
            : "Subtitles: Auto (" + languageName + ")";
        subtitleOptions.Add(new SubtitleOption(SubtitleOption.AutoId, autoLabel));
        subtitleOptions.Add(new SubtitleOption(-1, "Subtitles: Off"));
        foreach (var track in tracks) subtitleOptions.Add(track);
        foreach (var option in subtitleOptions) subtitleChoice.Items.Add(option);

        int selectedIndex;
        if (subtitleSelection == SubtitleOption.AutoId) selectedIndex = 0;
        else if (subtitleSelection < 0) selectedIndex = 1;
        else
        {
            selectedIndex = subtitleOptions.FindIndex(x => x.Id == subtitleSelection);
            if (selectedIndex < 0)
            {
                subtitleSelection = SubtitleOption.AutoId;
                selectedIndex = 0;
            }
        }
        subtitleChoice.SelectedIndex = selectedIndex;
        subtitleChoice.Enabled = tracks.Count > 0;
        subtitleChoice.EmptyText = tracks.Count > 0 ? autoLabel : "No subtitles";
        suppressSubtitleChange = false;

        if (subtitleSelection == SubtitleOption.AutoId) ApplyAutoSubtitle(activePlayer, tracks);
        else activePlayer.SetSpu(subtitleSelection);
    }

    static string CleanSubtitleName(string? name, int id)
    {
        string value = (name ?? "").Trim();
        if (value.Length == 0 || value.Equals("Track", StringComparison.OrdinalIgnoreCase)) value = "Subtitle " + id;
        return value.Replace("\t", " ");
    }

    static void ApplyAutoSubtitle(MediaPlayer activePlayer, IReadOnlyList<SubtitleOption> tracks)
    {
        int preferred = ChoosePreferredSubtitleId(tracks, System.Globalization.CultureInfo.CurrentUICulture);
        if (preferred < 0 && activePlayer.Spu >= 0 && tracks.Any(t => t.Id == activePlayer.Spu)) preferred = activePlayer.Spu;
        activePlayer.SetSpu(preferred);
    }

    static int ChoosePreferredSubtitleId(IReadOnlyList<SubtitleOption> tracks, System.Globalization.CultureInfo culture)
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
            if (label.Contains("dialog", StringComparison.OrdinalIgnoreCase) || label.Contains("full", StringComparison.OrdinalIgnoreCase)) score += 3;
            if (label.Contains("sign", StringComparison.OrdinalIgnoreCase) || label.Contains("song", StringComparison.OrdinalIgnoreCase)) score -= 5;
            if (label.Contains("forced", StringComparison.OrdinalIgnoreCase)) score -= 3;
            if (label.Contains("commentary", StringComparison.OrdinalIgnoreCase)) score -= 8;
            if (score > bestScore && score > 0) { bestScore = score; bestId = track.Id; }
        }
        return bestId;
    }

    void ApplySubtitleSelection()
    {
        if (suppressSubtitleChange || player == null) return;
        int index = subtitleChoice.SelectedIndex;
        if (index < 0 || index >= subtitleOptions.Count) return;
        SubtitleOption option = subtitleOptions[index];
        subtitleSelection = option.Id;
        if (option.Id == SubtitleOption.AutoId)
            ApplyAutoSubtitle(player, subtitleOptions.Where(t => t.Id >= 0).ToList());
        else
            player.SetSpu(option.Id);
    }

    static List<ITorrentManagerFile> FindMatchingSubtitleFiles(IEnumerable<ITorrentManagerFile> files, ITorrentManagerFile videoFile, bool singleVideo)
    {
        return files
            .Where(file => SubtitleFiles.Contains(Path.GetExtension(file.Path)))
            .Where(file => SubtitleBelongsToVideo(videoFile.Path, file.Path, singleVideo))
            .OrderBy(file => file.Path, TorrentVideoSelection.NaturalPathComparer)
            .ToList();
    }

    static bool SubtitleBelongsToVideo(string videoPath, string subtitlePath, bool singleVideo)
    {
        string videoNormalized = videoPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string subtitleNormalized = subtitlePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string videoDir = Path.GetDirectoryName(videoNormalized) ?? "";
        string subtitleDir = Path.GetDirectoryName(subtitleNormalized) ?? "";
        bool sameDirectory = string.Equals(videoDir, subtitleDir, StringComparison.OrdinalIgnoreCase);
        bool subtitleFolder = !sameDirectory && string.Equals(Path.GetDirectoryName(subtitleDir) ?? "", videoDir, StringComparison.OrdinalIgnoreCase)
            && (Path.GetFileName(subtitleDir).Contains("sub", StringComparison.OrdinalIgnoreCase));
        if (!sameDirectory && !subtitleFolder && !singleVideo) return false;

        string videoIdentity = MediaIdentity(videoNormalized);
        string subtitleIdentity = MediaIdentity(subtitleNormalized);
        if (videoIdentity.Length > 0 && subtitleIdentity.Length > 0 && string.Equals(videoIdentity, subtitleIdentity, StringComparison.OrdinalIgnoreCase)) return true;
        if (videoIdentity.Length > 5 && subtitleIdentity.StartsWith(videoIdentity + " ", StringComparison.OrdinalIgnoreCase)) return true;
        if (subtitleIdentity.Length > 5 && videoIdentity.StartsWith(subtitleIdentity + " ", StringComparison.OrdinalIgnoreCase)) return true;

        string videoEpisode = EpisodeIdentity(Path.GetFileNameWithoutExtension(videoNormalized));
        string subtitleEpisode = EpisodeIdentity(Path.GetFileNameWithoutExtension(subtitleNormalized));
        if (videoEpisode.Length > 0 && videoEpisode.Equals(subtitleEpisode, StringComparison.OrdinalIgnoreCase))
        {
            var videoWords = IdentityWords(videoIdentity);
            var subtitleWords = IdentityWords(subtitleIdentity);
            return videoWords.Count == 0 || subtitleWords.Count == 0 || videoWords.Intersect(subtitleWords, StringComparer.OrdinalIgnoreCase).Any();
        }

        return singleVideo && (sameDirectory || subtitleFolder);
    }

    static string MediaIdentity(string path)
    {
        string value = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        value = System.Text.RegularExpressions.Regex.Replace(value, @"\[[^\]]*\]|\([^\)]*\)", " ");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            @"\b(?:2160p|1440p|1080p|720p|576p|480p|x264|x265|h264|h265|hevc|av1|aac|ac3|eac3|flac|opus|web[ ._-]?dl|webrip|bluray|bdrip|hdr|dv|dual|multi|audio|subbed|eng|english|jpn|japanese|forced|signs?|songs?|sdh)\b",
            " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        value = System.Text.RegularExpressions.Regex.Replace(value, @"[^\p{L}\p{N}]+", " ").Trim();
        return System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ");
    }

    static HashSet<string> IdentityWords(string value)
        => value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 1 && !x.All(char.IsDigit))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    static string EpisodeIdentity(string value)
    {
        var seasonEpisode = System.Text.RegularExpressions.Regex.Match(value, @"\bS(?<s>\d{1,2})[ ._-]*E(?<e>\d{1,3})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (seasonEpisode.Success) return "s" + seasonEpisode.Groups["s"].Value.TrimStart('0') + "e" + seasonEpisode.Groups["e"].Value.TrimStart('0');
        var namedEpisode = System.Text.RegularExpressions.Regex.Match(value, @"\b(?:EP|Episode)[ ._-]*(?<e>\d{1,4})(?:v\d+)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (namedEpisode.Success) return "e" + namedEpisode.Groups["e"].Value.TrimStart('0');
        var animeEpisode = System.Text.RegularExpressions.Regex.Match(value, @"\s-\s(?<e>\d{1,4})(?:v\d+)?(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return animeEpisode.Success ? "e" + animeEpisode.Groups["e"].Value.TrimStart('0') : "";
    }

    async Task LoadExternalSubtitlesAsync(IReadOnlyList<ITorrentManagerFile> subtitles, int request)
    {
        if (subtitles.Count == 0) return;
        foreach (var subtitle in subtitles)
        {
            try
            {
                var clock = Stopwatch.StartNew();
                while (subtitle.BitField.PercentComplete < 99.9 && clock.Elapsed < TimeSpan.FromSeconds(90))
                {
                    if (request != playbackRevision || closing || cts.IsCancellationRequested) return;
                    await Task.Delay(250, cts.Token);
                }
                if (request != playbackRevision || closing || subtitle.BitField.PercentComplete < 99.9) continue;

                string path = File.Exists(subtitle.DownloadCompleteFullPath) ? subtitle.DownloadCompleteFullPath : subtitle.FullPath;
                if (!File.Exists(path) || player == null) continue;
                if (player.AddSlave(MediaSlaveType.Subtitle, new Uri(path).AbsoluteUri, false))
                {
                    subtitleSignature = "";
                    RefreshSubtitleChoices();
                }
            }
            catch (OperationCanceledException) { return; }
            catch { }
        }
    }

'''
s = s[:start] + new_block + s[end:]

# Add volume behaviour before seeking helpers.
rep(
'''    void CommitSeek(){if(player != null && player.Length > 0)player.Time = (long)(player.Length * (seek.Value / 1000d));}
''',
'''    void SetVolume(int percent)
    {
        currentVolume = Math.Max(0, Math.Min(100, percent));
        if (currentVolume > 0) lastAudibleVolume = currentVolume;
        if (player != null) player.Volume = currentVolume;
        UpdateVolumeUi();
    }

    void UpdateVolumeUi()
    {
        volume.Value = currentVolume * 10;
        volumeLabel.Text = currentVolume + "%";
        mute.Text = currentVolume == 0 ? "Unmute" : "Mute";
    }

    void CommitVolume() => SetVolume((int)Math.Round(volume.Value / 10d));

    void ToggleMute()
    {
        if (currentVolume > 0) SetVolume(0);
        else SetVolume(Math.Max(1, lastAudibleVolume));
    }

    void CommitSeek(){if(player != null && player.Length > 0)player.Time = (long)(player.Length * (seek.Value / 1000d));}
''')

p.write_text(s, encoding='utf-8')
