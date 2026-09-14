using System.Globalization;
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
    DateTime nextTrackRefreshUtc = DateTime.MinValue;

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

    internal static double ChooseConservativeObservedRate(double measuredBytesPerSecond, double swarmBytesPerSecond)
    {
        measuredBytesPerSecond = Math.Max(0, measuredBytesPerSecond);
        swarmBytesPerSecond = Math.Max(0, swarmBytesPerSecond);
        if (measuredBytesPerSecond > 0 && swarmBytesPerSecond > 0)
            return Math.Min(measuredBytesPerSecond, swarmBytesPerSecond);
        return Math.Max(measuredBytesPerSecond, swarmBytesPerSecond);
    }

    async Task<bool> WaitForPlaybackGateOnCloseAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            await playbackGate.WaitAsync(deadline.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
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
        double conservative = ChooseConservativeObservedRate(40d * 1024 * 1024, 1d * 1024 * 1024);
        if (conservative > 1.01d * 1024 * 1024)
            throw new InvalidOperationException("Adaptive throughput estimation became optimistic compared with the swarm rate.");
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
