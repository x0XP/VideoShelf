using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using MonoTorrent;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal sealed class StreamingTorrentSession : IAsyncDisposable, IDisposable
{
    const int MaxMetadataBytes = 16 * 1024 * 1024;
    static readonly TimeSpan SessionStopTimeout = TimeSpan.FromSeconds(2);
    static readonly HttpClient Http = CreateHttp();
    readonly string stateRoot;
    readonly string temporaryRoot;
    readonly string httpPrefix;
    string? metadataFile;

    public ClientEngine Engine { get; }
    public TorrentManager? Manager { get; private set; }

    public StreamingTorrentSession(string stateRoot, string temporaryRoot)
    {
        this.stateRoot = stateRoot;
        this.temporaryRoot = temporaryRoot;
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(temporaryRoot);
        httpPrefix = $"http://127.0.0.1:{FindFreePort()}/";
        var builder = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AllowLocalPeerDiscovery = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = false,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = stateRoot,
            HttpStreamingPrefix = httpPrefix,
            MaximumConnections = 300,
            MaximumHalfOpenConnections = 32,
            DiskCacheBytes = 32 * 1024 * 1024,
            MaximumDownloadRate = 0
        };
        Engine = new ClientEngine(builder.ToSettings());
    }

    static TorrentSettings StreamingSettings()
    {
        return new TorrentSettingsBuilder
        {
            AllowDht = true,
            AllowPeerExchange = true,
            MaximumConnections = 160,
            MaximumDownloadRate = 0,
            MaximumUploadRate = 0,
            UploadSlots = 8
        }.ToSettings();
    }

    static HttpClient CreateHttp()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
        return client;
    }

    static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task<TorrentManager> AddAsync(string source, string destination, CancellationToken token)
    {
        TorrentSettings settings = StreamingSettings();
        if (MagnetLink.TryParse(source, out MagnetLink? magnet) && magnet != null)
        {
            Manager = await Engine.AddStreamingAsync(magnet, destination, settings);
            return Manager;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("This result does not contain a usable magnet link or torrent metadata URL.");

        metadataFile = Path.Combine(temporaryRoot, "selected-" + Guid.NewGuid().ToString("N") + ".torrent");
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > MaxMetadataBytes)
            throw new InvalidOperationException("Torrent metadata is unexpectedly large.");

        await using (var input = await response.Content.ReadAsStreamAsync(token))
        await using (var output = new FileStream(metadataFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            byte[] buffer = new byte[81920];
            int total = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0) break;
                total += read;
                if (total > MaxMetadataBytes) throw new InvalidOperationException("Torrent metadata is unexpectedly large.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }

        Manager = await Engine.AddStreamingAsync(metadataFile, destination, settings);
        return Manager;
    }

    public string StreamingUrl(string relativeUri) => httpPrefix.TrimEnd('/') + "/" + relativeUri.TrimStart('/');

    public async ValueTask DisposeAsync()
    {
        if (Manager != null)
        {
            try
            {
                using var deadline = new CancellationTokenSource(SessionStopTimeout + TimeSpan.FromSeconds(1));
                await Manager.StopAsync(SessionStopTimeout).WaitAsync(deadline.Token);
            }
            catch { }
        }
        Engine.Dispose();
        if (metadataFile != null) try { File.Delete(metadataFile); } catch { }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public static void SelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(), "VideoShelf-stream-settings-" + Guid.NewGuid().ToString("N"));
        string state = Path.Combine(root, "state");
        string temporary = Path.Combine(root, "temporary");
        Directory.CreateDirectory(root);
        try
        {
            using var session = new StreamingTorrentSession(state, temporary);
            if (session.Engine.Settings.MaximumConnections < 250 || session.Engine.Settings.MaximumHalfOpenConnections < 24)
                throw new InvalidOperationException("Streaming engine connection tuning regressed.");
            if (session.Engine.Settings.AutoSaveLoadFastResume)
                throw new InvalidOperationException("Streaming fast-resume must stay disabled because video payloads are temporary.");
            if (!Path.GetFullPath(session.Engine.Settings.CacheDirectory).Equals(Path.GetFullPath(state), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Streaming discovery state is not using the persistent cache path.");
            if (SessionStopTimeout > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException("Streaming session shutdown must remain bounded.");
            if (OptimizedStreamForm.CalculatePrebufferBytes(1024L * 1024 * 1024) < 8L * 1024 * 1024)
                throw new InvalidOperationException("Streaming prebuffer target is too small.");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

internal sealed class OptimizedStreamForm : Form
{
    static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".flv", ".vob" };
    static readonly TimeSpan InitialMetadataWait = TimeSpan.FromSeconds(20);
    static readonly TimeSpan RetryMetadataWait = TimeSpan.FromSeconds(30);
    static readonly TimeSpan MetadataRetryStopTimeout = TimeSpan.FromSeconds(2);
    readonly string source;
    readonly string? fallbackSource;
    readonly string releaseTitle;
    readonly bool fixtureMode;
    readonly Panel videoHost = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly VideoView video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly Label videoOverlay = Theme.Label("Preparing stream…", true);
    readonly DarkComboBox fileChoice = new() { Width = 390 };
    readonly DarkComboBox subtitleChoice = new() { Width = 180, EmptyText = "Subtitles" };
    readonly Label status = Theme.Label("Preparing torrent metadata…"), timeLabel = Theme.Label("00:00 / 00:00");
    readonly Button playPause = Theme.Button("Play", 90), stop = Theme.Button("Stop", 80);
    readonly SeekBar seek = new() { Width = 280, Enabled = false };
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 350 };
    readonly CancellationTokenSource cts = new();
    readonly SemaphoreSlim playbackGate = new(1, 1);
    readonly string cache;
    readonly string discoveryCache;
    StreamingTorrentSession? session;
    TorrentManager? manager;
    LibVLC? vlc;
    MediaPlayer? player;
    Media? currentMedia;
    object? currentHttpStream;
    List<ITorrentManagerFile> playable = new();
    ITorrentManagerFile? selected;
    readonly List<SubtitleOption> subtitleOptions = new();
    string subtitleSignature = "";
    bool suppressSubtitleChange;
    bool subtitleAutoApplied;
    int playbackRevision;
    bool closing;

    sealed class SubtitleOption
    {
        public const int AutoId = int.MinValue;
        public int Id { get; }
        public string Name { get; }
        public SubtitleOption(int id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }

    public OptimizedStreamForm(string source, string title) : this(source, title, null, false) { }
    internal OptimizedStreamForm(string source, string title, string? fallbackSource) : this(source, title, fallbackSource, false) { }
    internal OptimizedStreamForm(string source, string title, bool fixtureMode) : this(source, title, null, fixtureMode) { }

    OptimizedStreamForm(string source, string title, string? fallbackSource, bool fixtureMode)
    {
        this.source = source;
        this.fallbackSource = string.IsNullOrWhiteSpace(fallbackSource) ? null : fallbackSource;
        releaseTitle = string.IsNullOrWhiteSpace(title) ? "Torrent" : title;
        this.fixtureMode = fixtureMode;
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf");
        cache = Path.Combine(appData, "StreamingCache", Guid.NewGuid().ToString("N"));
        discoveryCache = Path.Combine(appData, "TorrentDiscovery");
        Theme.Form(this, "Stream locally", new Size(1060, 720)); MinimumSize = new Size(800, 560);
        var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Background };
        var accentTop = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Blue };
        var accentLeft = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Red };
        var heading = Theme.Label(releaseTitle, true); heading.SetBounds(22, 14, 900, 27); heading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; heading.AutoEllipsis = true;
        var fileLabel = Theme.Label("Video"); fileLabel.SetBounds(22, 54, 42, 26);
        fileChoice.SetBounds(70, 52, 430, 31); fileChoice.Enabled = false;
        status.SetBounds(520, 54, 500, 25); status.Anchor = AnchorStyles.Top | AnchorStyles.Left; status.AutoEllipsis = true;
        top.Controls.AddRange(new Control[] { accentTop, accentLeft, heading, fileLabel, fileChoice, status });

        videoOverlay.Dock = DockStyle.Fill; videoOverlay.TextAlign = ContentAlignment.MiddleCenter; videoOverlay.BackColor = Color.Black; videoOverlay.ForeColor = Theme.Muted; videoOverlay.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        if (fixtureMode)
        {
            videoHost.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Black });
        }
        else
        {
            video.Visible = false;
            videoHost.Controls.Add(video);
        }
        videoHost.Controls.Add(videoOverlay); videoOverlay.BringToFront();

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Theme.Background };
        playPause.SetBounds(22, 16, 90, 34); playPause.Enabled = false;
        stop.SetBounds(122, 16, 80, 34);
        subtitleChoice.SetBounds(216, 18, 180, 31); subtitleChoice.Enabled = false;
        seek.SetBounds(410, 18, 460, 30); seek.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        timeLabel.SetBounds(880, 22, 150, 22); timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left; timeLabel.TextAlign = ContentAlignment.MiddleRight; timeLabel.ForeColor = Color.FromArgb(190, 205, 220);
        bottom.Controls.AddRange(new Control[] { playPause, stop, subtitleChoice, seek, timeLabel });
        Controls.Add(videoHost); Controls.Add(bottom); Controls.Add(top);

        if (fixtureMode)
        {
            ConfigureFixture();
            FormClosed += (_, _) =>
            {
                uiTimer.Dispose();
                cts.Dispose();
                playbackGate.Dispose();
            };
        }
        else
        {
            Shown += async (_, _) => await StartAsync();
            FormClosing += OnClosing;
            fileChoice.SelectedIndexChanged += async (_, _) =>
            {
                if (fileChoice.SelectedIndex >= 0 && fileChoice.SelectedIndex < playable.Count && playable[fileChoice.SelectedIndex] != selected)
                    await PlayFileAsync(playable[fileChoice.SelectedIndex]);
            };
        }
        subtitleChoice.SelectedIndexChanged += (_, _) => ApplySubtitleSelection();
        playPause.Click += (_, _) => TogglePlayback();
        stop.Click += (_, _) => Close();
        seek.ValueCommitted += (_, _) => CommitSeek();
        uiTimer.Tick += (_, _) => RefreshStats();
        Resize += (_, _) => LayoutPlayer();
        LayoutPlayer();
    }

    void ConfigureFixture()
    {
        fileChoice.Items.Add("Season 01/Example.S01E01.1080p.mkv  (1.4 GB)");
        fileChoice.Items.Add("Season 01/Example.S01E02.1080p.mkv  (1.3 GB)");
        fileChoice.Items.Add("Season 01/Example.S01E03.1080p.mkv  (1.5 GB)");
        fileChoice.SelectedIndex = 0;
        fileChoice.Enabled = true;
        subtitleOptions.Add(new SubtitleOption(SubtitleOption.AutoId, "Subtitles: Auto (English)"));
        subtitleOptions.Add(new SubtitleOption(-1, "Subtitles: Off"));
        subtitleOptions.Add(new SubtitleOption(11, "English"));
        subtitleOptions.Add(new SubtitleOption(12, "English • Signs & Songs"));
        foreach (var option in subtitleOptions) subtitleChoice.Items.Add(option);
        subtitleChoice.SelectedIndex = 0;
        subtitleChoice.Enabled = true;
        status.Text = "Ready • 4.8 MB/s • temporary stream cache";
        videoOverlay.Text = "Optimized local stream preview";
        playPause.Text = "Pause";
        playPause.Enabled = true;
        seek.Enabled = true;
        seek.Value = 340;
        timeLabel.Text = "8:12 / 24:06";
    }

    void LayoutPlayer()
    {
        status.Width = Math.Max(180, ClientSize.Width - status.Left - 22);
        int timeWidth = 150, timeX = Math.Max(620, ClientSize.Width - 22 - timeWidth);
        int subtitleWidth = ClientSize.Width < 900 ? 150 : 180;
        subtitleChoice.SetBounds(216, 18, subtitleWidth, 31);
        int seekX = subtitleChoice.Right + 14;
        timeLabel.SetBounds(timeX, 22, timeWidth, 22);
        seek.SetBounds(seekX, 18, Math.Max(120, timeX - seekX - 12), 30);
    }

    async Task StartAsync()
    {
        try
        {
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(discoveryCache);
            var streamSession = new StreamingTorrentSession(discoveryCache, Path.Combine(cache, "metadata"));
            session = streamSession;
            TorrentManager torrentManager;
            try
            {
                torrentManager = await streamSession.AddAsync(source, cache, cts.Token);
            }
            catch (Exception) when (!cts.IsCancellationRequested && fallbackSource != null)
            {
                status.Text = "Direct metadata unavailable • using peer discovery";
                videoOverlay.Text = "Direct torrent metadata was unavailable…\n\nFalling back to peer discovery";
                await streamSession.DisposeAsync();
                streamSession = new StreamingTorrentSession(discoveryCache, Path.Combine(cache, "metadata-fallback"));
                session = streamSession;
                torrentManager = await streamSession.AddAsync(fallbackSource, cache, cts.Token);
            }
            manager = torrentManager;
            bool metadataWasCached = torrentManager.HasMetadata;
            status.Text = metadataWasCached ? "Metadata cached • connecting" : "Connecting • retrieving metadata";
            videoOverlay.Text = metadataWasCached ? "Preparing cached torrent metadata…" : "Retrieving torrent metadata…";
            await torrentManager.StartAsync();
            if (!torrentManager.HasMetadata && !await WaitForMetadataWithTimeoutAsync(torrentManager, InitialMetadataWait, cts.Token))
            {
                status.Text = "Retrying torrent metadata discovery…";
                videoOverlay.Text = "Metadata is taking longer than expected…\n\nRetrying peer discovery";
                await StopForMetadataRetryAsync(torrentManager, cts.Token);
                await Task.Delay(350, cts.Token);
                await torrentManager.StartAsync();
                if (!await WaitForMetadataWithTimeoutAsync(torrentManager, RetryMetadataWait, cts.Token))
                    throw new InvalidOperationException("Torrent metadata could not be retrieved after retrying. The swarm may currently have no reachable peers; try again later or choose another result.");
            }
            playable = torrentManager.Files
                .Where(f => Playable.Contains(Path.GetExtension(f.Path)))
                .OrderBy(f => f.Path, TorrentVideoSelection.NaturalPathComparer)
                .ToList();
            if (playable.Count == 0) throw new InvalidOperationException("This torrent does not contain a supported video file.");
            foreach (var file in torrentManager.Files) await torrentManager.SetFilePriorityAsync(file, Priority.DoNotDownload);
            fileChoice.BeginUpdate();
            foreach (var file in playable) fileChoice.Items.Add($"{file.Path}  ({FormatSize(file.Length)})");
            fileChoice.EndUpdate(); fileChoice.Enabled = true;

            Core.Initialize();
            var localVlc = new LibVLC("--no-video-title-show", "--network-caching=5000", "--file-caching=5000", "--clock-jitter=0", "--clock-synchro=0");
            vlc = localVlc;
            var mediaPlayer = new MediaPlayer(localVlc);
            player = mediaPlayer;
            video.MediaPlayer = mediaPlayer; uiTimer.Start();

            int target = TorrentVideoSelection.ChooseDefaultIndex(releaseTitle, playable.Select(f => f.Path).ToArray());
            fileChoice.SelectedIndex = target >= 0 && target < playable.Count ? target : 0;
        }
        catch (OperationCanceledException) { if (!closing) Close(); }
        catch (Exception ex)
        {
            video.Visible = false; videoOverlay.Visible = true; videoOverlay.Text = "Unable to start this stream"; status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "VideoShelf streaming", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static async Task StopForMetadataRetryAsync(TorrentManager torrentManager, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(MetadataRetryStopTimeout + TimeSpan.FromSeconds(1));
        try
        {
            await torrentManager.StopAsync(MetadataRetryStopTimeout).WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        catch when (!token.IsCancellationRequested) { }
    }

    static async Task<bool> WaitForMetadataWithTimeoutAsync(TorrentManager torrentManager, TimeSpan timeout, CancellationToken token)
    {
        if (torrentManager.HasMetadata) return true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        linked.CancelAfter(timeout);
        try
        {
            await torrentManager.WaitForMetadataAsync(linked.Token);
            return torrentManager.HasMetadata;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return torrentManager.HasMetadata;
        }
    }

    internal static long CalculatePrebufferBytes(long fileLength)
    {
        const long min = 8L * 1024 * 1024, max = 20L * 1024 * 1024;
        if (fileLength <= 0) return min;
        long target = fileLength / 64;
        target = Math.Max(min, Math.Min(max, target));
        return Math.Min(fileLength, target);
    }

    async Task PrebufferAsync(ITorrentManagerFile file, int request)
    {
        TorrentManager? activeManager = manager;
        if (activeManager == null) return;
        var streamProvider = activeManager.StreamProvider ?? throw new InvalidOperationException("Torrent streaming provider is unavailable.");
        long target = CalculatePrebufferBytes(file.Length), readTotal = 0;
        byte[] buffer = new byte[256 * 1024];
        videoOverlay.Visible = true; video.Visible = false;
        using Stream warm = await streamProvider.CreateStreamAsync(file, false, cts.Token);
        var clock = Stopwatch.StartNew();
        while (readTotal < target)
        {
            if (request != playbackRevision || closing) throw new OperationCanceledException();
            int want = (int)Math.Min(buffer.Length, target - readTotal);
            int read = await warm.ReadAsync(buffer.AsMemory(0, want), cts.Token);
            if (read <= 0) break;
            readTotal += read;
            double mb = readTotal / (1024d * 1024d), totalMb = target / (1024d * 1024d);
            double measured = clock.Elapsed.TotalSeconds > 0.5 ? mb / clock.Elapsed.TotalSeconds : 0;
            double swarm = activeManager.Monitor.DownloadRate / (1024d * 1024d);
            double rate = Math.Max(measured, swarm);
            status.Text = $"Buffering {mb:0.0}/{totalMb:0.0} MB • {rate:0.0} MB/s";
            videoOverlay.Text = $"Buffering selected video…\n\n{mb:0.0} / {totalMb:0.0} MB";
        }
    }

    async Task PlayFileAsync(ITorrentManagerFile file)
    {
        int request = Interlocked.Increment(ref playbackRevision);
        try
        {
            await playbackGate.WaitAsync(cts.Token);
            try
            {
                var activeManager = manager;
                var activeSession = session;
                var activePlayer = player;
                var activeVlc = vlc;
                if (request != playbackRevision || closing || activeManager == null || activeSession == null || activePlayer == null || activeVlc == null) return;
                var streamProvider = activeManager.StreamProvider ?? throw new InvalidOperationException("Torrent streaming provider is unavailable.");
                selected = file; playPause.Enabled = false; seek.Enabled = false;
                ResetSubtitleChoices();
                try { activePlayer.Stop(); } catch { }
                currentMedia?.Dispose(); currentMedia = null; await DisposeHttpStreamAsync();
                foreach (var item in activeManager.Files) await activeManager.SetFilePriorityAsync(item, item == file ? Priority.High : Priority.DoNotDownload);

                await PrebufferAsync(file, request);
                if (request != playbackRevision || closing) return;
                status.Text = "Opening buffered stream…";
                var httpStream = await streamProvider.CreateHttpStreamAsync(file, true, cts.Token);
                if (request != playbackRevision || closing) { await DisposeObjectAsync(httpStream); return; }
                currentHttpStream = httpStream;
                currentMedia = new Media(activeVlc, new Uri(activeSession.StreamingUrl(httpStream.RelativeUri)));
                bool started = activePlayer.Play(currentMedia);
                if (!started) throw new InvalidOperationException("LibVLC could not start playback for the selected torrent file.");
                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;
                RefreshSubtitleChoices();
            }
            finally { playbackGate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (closing) return;
            status.Text = "Playback failed • " + ex.Message; video.Visible = false; videoOverlay.Text = "Playback failed\n\n" + ex.Message; videoOverlay.Visible = true;
            playPause.Text = "Play"; playPause.Enabled = player != null;
        }
    }

    void ResetSubtitleChoices()
    {
        suppressSubtitleChange = true;
        subtitleChoice.Items.Clear();
        subtitleOptions.Clear();
        subtitleChoice.Enabled = false;
        subtitleChoice.EmptyText = "Subtitles";
        subtitleSignature = "";
        subtitleAutoApplied = false;
        suppressSubtitleChange = false;
    }

    void RefreshSubtitleChoices()
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

        string nextSignature = string.Join("", tracks.Select(t => t.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + t.Name));
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
        subtitleChoice.SelectedIndex = 0;
        subtitleChoice.Enabled = tracks.Count > 0;
        subtitleChoice.EmptyText = tracks.Count > 0 ? autoLabel : "No subtitles";
        suppressSubtitleChange = false;

        if (!subtitleAutoApplied)
        {
            ApplyAutoSubtitle(activePlayer, tracks);
            subtitleAutoApplied = true;
        }
    }

    static string CleanSubtitleName(string? name, int id)
    {
        string value = (name ?? "").Trim();
        if (value.Length == 0 || value.Equals("Track", StringComparison.OrdinalIgnoreCase)) value = "Subtitle " + id;
        return value.Replace("	", " ");
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
            var tokens = System.Text.RegularExpressions.Regex.Split(label, "[^\p{L}\p{N}]+")
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
        if (option.Id == SubtitleOption.AutoId)
        {
            ApplyAutoSubtitle(player, subtitleOptions.Where(t => t.Id >= 0).ToList());
            subtitleAutoApplied = true;
            return;
        }
        player.SetSpu(option.Id);
        subtitleAutoApplied = true;
    }

    void TogglePlayback()
    {
        if (player == null || currentMedia == null) return;
        if (player.IsPlaying) { player.Pause(); playPause.Text = "Play"; }
        else { video.Visible = true; player.Play(); playPause.Text = "Pause"; videoOverlay.Visible = false; }
    }

    void CommitSeek(){if(player != null && player.Length > 0)player.Time = (long)(player.Length * (seek.Value / 1000d));}

    void RefreshStats()
    {
        RefreshSubtitleChoices();
        if (manager != null && selected != null && currentMedia != null)
            status.Text = $"{manager.State} • {manager.Monitor.DownloadRate / (1024d * 1024d):0.0} MB/s • buffered local stream";
        if (player != null && player.Length > 0)
        {
            if (!seek.Focused) seek.Value = Math.Max(0, Math.Min(1000, (int)(player.Time * 1000d / player.Length)));
            timeLabel.Text = $"{FormatTime(player.Time)} / {FormatTime(player.Length)}";
            if (!player.IsPlaying && player.Time > 0 && player.Time < player.Length - 1000) playPause.Text = "Play";
        }
    }

    async ValueTask DisposeHttpStreamAsync(){object? old=currentHttpStream;currentHttpStream=null;if(old!=null)await DisposeObjectAsync(old);}
    static async ValueTask DisposeObjectAsync(object value){try{if(value is IAsyncDisposable a)await a.DisposeAsync();else if(value is IDisposable d)d.Dispose();}catch{}}
    static string FormatTime(long ms){if(ms<0)ms=0;TimeSpan t=TimeSpan.FromMilliseconds(ms);return t.TotalHours>=1?t.ToString(@"h\:mm\:ss"):t.ToString(@"m\:ss");}
    static string FormatSize(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/(1024d*1024d*1024d):0.0} GB":$"{bytes/(1024d*1024d):0.0} MB";
    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing || fixtureMode) return;
        closing=true;e.Cancel=true;Interlocked.Increment(ref playbackRevision);cts.Cancel();uiTimer.Stop();
        try{await playbackGate.WaitAsync();}catch{}
        try
        {
            try{player?.Stop();}catch{}
            currentMedia?.Dispose();currentMedia=null;await DisposeHttpStreamAsync();video.MediaPlayer=null;player?.Dispose();vlc?.Dispose();
            if(session!=null)await session.DisposeAsync();
        }
        finally{try{playbackGate.Release();}catch{}}
        playbackGate.Dispose();cts.Dispose();try{Directory.Delete(cache,true);}catch{}
        e.Cancel=false;BeginInvoke(Close);
    }
}
