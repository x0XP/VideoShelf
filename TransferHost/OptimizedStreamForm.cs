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
    static readonly HttpClient Http = CreateHttp();
    readonly string cacheRoot;
    readonly string httpPrefix;
    string? metadataFile;

    public ClientEngine Engine { get; }
    public TorrentManager? Manager { get; private set; }

    public StreamingTorrentSession(string cacheRoot)
    {
        this.cacheRoot = cacheRoot;
        Directory.CreateDirectory(cacheRoot);
        httpPrefix = $"http://127.0.0.1:{FindFreePort()}/";
        var builder = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AllowLocalPeerDiscovery = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cacheRoot,
            HttpStreamingPrefix = httpPrefix,
            MaximumConnections = 300,
            MaximumHalfOpenConnections = 32,
            ConnectionTimeout = TimeSpan.FromSeconds(8),
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
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
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

        metadataFile = Path.Combine(cacheRoot, "selected.torrent");
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
        if (Manager != null) try { await Manager.StopAsync(); } catch { }
        Engine.Dispose();
        if (metadataFile != null) try { File.Delete(metadataFile); } catch { }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public static void SelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(), "VideoShelf-stream-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var session = new StreamingTorrentSession(root);
            if (session.Engine.Settings.MaximumConnections < 250 || session.Engine.Settings.MaximumHalfOpenConnections < 24)
                throw new InvalidOperationException("Streaming engine connection tuning regressed.");
            if (OptimizedStreamForm.CalculatePrebufferBytes(1024L * 1024 * 1024) < 8L * 1024 * 1024)
                throw new InvalidOperationException("Streaming prebuffer target is too small.");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

internal sealed class OptimizedStreamForm : Form
{
    static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".flv", ".vob" };
    readonly string source;
    readonly string releaseTitle;
    readonly bool fixtureMode;
    readonly Panel videoHost = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly VideoView video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly Label videoOverlay = Theme.Label("Preparing stream…", true);
    readonly DarkComboBox fileChoice = new() { Width = 390 };
    readonly Label status = Theme.Label("Preparing torrent metadata…"), timeLabel = Theme.Label("00:00 / 00:00");
    readonly Button playPause = Theme.Button("Play", 90), stop = Theme.Button("Stop", 80);
    readonly SeekBar seek = new() { Width = 280, Enabled = false };
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 350 };
    readonly CancellationTokenSource cts = new();
    readonly SemaphoreSlim playbackGate = new(1, 1);
    readonly string cache;
    StreamingTorrentSession? session;
    TorrentManager? manager;
    LibVLC? vlc;
    MediaPlayer? player;
    Media? currentMedia;
    object? currentHttpStream;
    List<ITorrentManagerFile> playable = new();
    ITorrentManagerFile? selected;
    int playbackRevision;
    bool closing;

    public OptimizedStreamForm(string source, string title) : this(source, title, false) { }

    internal OptimizedStreamForm(string source, string title, bool fixtureMode)
    {
        this.source = source;
        releaseTitle = string.IsNullOrWhiteSpace(title) ? "Torrent" : title;
        this.fixtureMode = fixtureMode;
        cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf", "StreamingCache", Guid.NewGuid().ToString("N"));
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
        seek.SetBounds(220, 18, 650, 30); seek.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        timeLabel.SetBounds(880, 22, 150, 22); timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left; timeLabel.TextAlign = ContentAlignment.MiddleRight; timeLabel.ForeColor = Color.FromArgb(190, 205, 220);
        bottom.Controls.AddRange(new Control[] { playPause, stop, seek, timeLabel });
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
        int timeWidth = 150, timeX = Math.Max(420, ClientSize.Width - 22 - timeWidth);
        timeLabel.SetBounds(timeX, 22, timeWidth, 22);
        seek.SetBounds(220, 18, Math.Max(180, timeX - 232), 30);
    }

    async Task StartAsync()
    {
        try
        {
            Directory.CreateDirectory(cache);
            session = new StreamingTorrentSession(Path.Combine(cache, "metadata"));
            manager = await session.AddAsync(source, cache, cts.Token);
            status.Text = "Connecting • retrieving metadata";
            videoOverlay.Text = "Retrieving torrent metadata…";
            await manager.StartAsync();
            await manager.WaitForMetadataAsync(cts.Token);
            playable = manager.Files
                .Where(f => Playable.Contains(Path.GetExtension(f.Path)))
                .OrderBy(f => f.Path, TorrentVideoSelection.NaturalPathComparer)
                .ToList();
            if (playable.Count == 0) throw new InvalidOperationException("This torrent does not contain a supported video file.");
            foreach (var file in manager.Files) await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);
            fileChoice.BeginUpdate();
            foreach (var file in playable) fileChoice.Items.Add($"{file.Path}  ({FormatSize(file.Length)})");
            fileChoice.EndUpdate(); fileChoice.Enabled = true;

            Core.Initialize();
            vlc = new LibVLC("--no-video-title-show", "--network-caching=5000", "--file-caching=5000", "--clock-jitter=0", "--clock-synchro=0");
            player = new MediaPlayer(vlc); video.MediaPlayer = player; uiTimer.Start();

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
        if (manager == null) return;
        long target = CalculatePrebufferBytes(file.Length), readTotal = 0;
        byte[] buffer = new byte[256 * 1024];
        videoOverlay.Visible = true; video.Visible = false;
        using Stream warm = await manager.StreamProvider.CreateStreamAsync(file, false, cts.Token);
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
            double swarm = manager.Monitor.DownloadRate / (1024d * 1024d);
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
                if (request != playbackRevision || closing || manager == null || session == null || player == null || vlc == null) return;
                selected = file; playPause.Enabled = false; seek.Enabled = false;
                try { player.Stop(); } catch { }
                currentMedia?.Dispose(); currentMedia = null; await DisposeHttpStreamAsync();
                foreach (var item in manager.Files) await manager.SetFilePriorityAsync(item, item == file ? Priority.High : Priority.DoNotDownload);

                await PrebufferAsync(file, request);
                if (request != playbackRevision || closing) return;
                status.Text = "Opening buffered stream…";
                var httpStream = await manager.StreamProvider.CreateHttpStreamAsync(file, true, cts.Token);
                if (request != playbackRevision || closing) { await DisposeObjectAsync(httpStream); return; }
                currentHttpStream = httpStream;
                currentMedia = new Media(vlc, new Uri(session.StreamingUrl(httpStream.RelativeUri)));
                bool started = player.Play(currentMedia);
                if (!started) throw new InvalidOperationException("LibVLC could not start playback for the selected torrent file.");
                playPause.Text = "Pause"; playPause.Enabled = true; seek.Enabled = true; video.Visible = true; videoOverlay.Visible = false;
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

    void TogglePlayback()
    {
        if (player == null || currentMedia == null) return;
        if (player.IsPlaying) { player.Pause(); playPause.Text = "Play"; }
        else { video.Visible = true; player.Play(); playPause.Text = "Pause"; videoOverlay.Visible = false; }
    }

    void CommitSeek(){if(player != null && player.Length > 0)player.Time = (long)(player.Length * (seek.Value / 1000d));}

    void RefreshStats()
    {
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
