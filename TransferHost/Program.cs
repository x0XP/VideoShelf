using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using MonoTorrent;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                TransferRuntime.SelfTest();
                return;
            }

            var command = args.FirstOrDefault()?.ToLowerInvariant();
            var values = Arguments.Parse(args.Skip(1).ToArray());
            string source = values.Required("source");
            string title = values.Get("title") ?? "Torrent";

            if (command == "download")
            {
                string destination = values.Required("destination");
                Application.Run(new DownloadForm(source, destination, title));
                return;
            }

            if (command == "stream")
            {
                Application.Run(new StreamForm(source, title));
                return;
            }

            throw new ArgumentException("Use download --source <magnet-or-torrent-url> --destination <folder> --title <title>, stream --source <magnet-or-torrent-url> --title <title>, or --self-test.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "VideoShelf", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }
}

internal sealed class Arguments
{
    readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    public static Arguments Parse(string[] args)
    {
        var result = new Arguments();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            string key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) result.values[key] = args[++i];
            else result.values[key] = "true";
        }
        return result;
    }
    public string? Get(string key) => values.TryGetValue(key, out var value) ? value : null;
    public string Required(string key) => Get(key) is { Length: > 0 } value ? value : throw new ArgumentException($"Missing --{key}.");
}

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(7, 9, 12);
    public static readonly Color Panel = Color.FromArgb(10, 12, 16);
    public static readonly Color Raised = Color.FromArgb(20, 23, 29);
    public static readonly Color Outline = Color.FromArgb(59, 65, 75);
    public static readonly Color Blue = Color.FromArgb(50, 156, 255);
    public static readonly Color Red = Color.FromArgb(255, 32, 32);
    public static readonly Color Text = Color.FromArgb(226, 231, 238);
    public static readonly Color Muted = Color.FromArgb(145, 153, 165);

    public static void Form(Form form, string title, Size size)
    {
        form.Text = "VideoShelf — " + title;
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10f);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = size;
        form.MinimumSize = size;
    }

    public static Button Button(string text, int width = 130)
    {
        var b = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Raised, ForeColor = Text, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Outline;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 34, 42);
        return b;
    }

    public static Label Label(string text, bool strong = false)
        => new() { Text = text, AutoSize = false, ForeColor = strong ? Color.White : Muted, Font = new Font("Segoe UI", strong ? 11f : 9.5f, strong ? FontStyle.Bold : FontStyle.Regular) };
}

internal sealed class TorrentSession : IAsyncDisposable
{
    const int MaxMetadataBytes = 16 * 1024 * 1024;
    static readonly HttpClient Http = CreateHttp();
    readonly string cacheRoot;
    readonly string? httpPrefix;
    string? metadataFile;

    public ClientEngine Engine { get; }
    public TorrentManager? Manager { get; private set; }

    public TorrentSession(string cacheRoot, bool streaming)
    {
        this.cacheRoot = cacheRoot;
        Directory.CreateDirectory(cacheRoot);
        httpPrefix = streaming ? $"http://127.0.0.1:{FindFreePort()}/" : null;
        var builder = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            HttpStreamingPrefix = httpPrefix
        };
        Engine = new ClientEngine(builder.ToSettings());
    }

    static HttpClient CreateHttp()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.6");
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

    public async Task<TorrentManager> AddAsync(string source, string destination, bool streaming, CancellationToken token)
    {
        if (MagnetLink.TryParse(source, out MagnetLink? magnet) && magnet != null)
        {
            Manager = streaming
                ? await Engine.AddStreamingAsync(magnet, destination)
                : await Engine.AddAsync(magnet, destination);
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

        Manager = streaming
            ? await Engine.AddStreamingAsync(metadataFile, destination)
            : await Engine.AddAsync(metadataFile, destination);
        return Manager;
    }

    public string StreamingUrl(string relativeUri)
        => httpPrefix is null ? throw new InvalidOperationException("This is not a streaming session.") : httpPrefix.TrimEnd('/') + "/" + relativeUri.TrimStart('/');

    public async ValueTask DisposeAsync()
    {
        if (Manager != null)
        {
            try { await Manager.StopAsync(); } catch { }
        }
        Engine.Dispose();
        if (metadataFile != null) try { File.Delete(metadataFile); } catch { }
    }

    public static void SelfTest()
    {
        if (!MagnetLink.TryParse("magnet:?xt=urn:btih:0123456789012345678901234567890123456789&dn=VideoShelfSelfTest", out _))
            throw new InvalidOperationException("MonoTorrent magnet parsing failed.");
        string root = Path.Combine(Path.GetTempPath(), "VideoShelf-transfer-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var engine = new ClientEngine(new EngineSettingsBuilder { HttpStreamingPrefix = $"http://127.0.0.1:{FindFreePort()}/" }.ToSettings());
            Core.Initialize();
            using var vlc = new LibVLC("--no-video-title-show");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

internal sealed class DownloadForm : Form
{
    readonly string source, destination, displayTitle;
    readonly Label state = Theme.Label("Preparing torrent metadata…"), details = Theme.Label("No media is transferred until this explicit download action."), destinationLabel = Theme.Label("");
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };
    readonly Button cancel = Theme.Button("Cancel", 105), openFolder = Theme.Button("Open folder", 120);
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 500 };
    readonly CancellationTokenSource cts = new();
    TorrentSession? session;
    TorrentManager? manager;
    bool complete, closing;

    public DownloadForm(string source, string destination, string title)
    {
        this.source = source; this.destination = destination; displayTitle = title;
        Theme.Form(this, "Download locally", new Size(700, 300));
        var accentTop = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Blue };
        var accentLeft = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Red };
        var heading = Theme.Label("DOWNLOAD LOCALLY", true); heading.SetBounds(28, 26, 620, 28);
        var name = Theme.Label(title, true); name.SetBounds(28, 62, 640, 28);
        destinationLabel.Text = destination; destinationLabel.SetBounds(28, 96, 640, 24); destinationLabel.AutoEllipsis = true;
        progress.SetBounds(28, 137, 640, 18);
        state.SetBounds(28, 170, 640, 24);
        details.SetBounds(28, 198, 640, 24);
        openFolder.SetBounds(438, 244, 120, 34); openFolder.Enabled = Directory.Exists(destination);
        cancel.SetBounds(568, 244, 100, 34);
        Controls.AddRange(new Control[] { accentTop, accentLeft, heading, name, destinationLabel, progress, state, details, openFolder, cancel });
        Shown += async (_, _) => await StartAsync();
        FormClosing += OnClosing;
        cancel.Click += (_, _) => { if (complete) Close(); else { cts.Cancel(); cancel.Enabled = false; state.Text = "Stopping…"; } };
        openFolder.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true }); } catch { } };
        uiTimer.Tick += (_, _) => RefreshStats();
    }

    async Task StartAsync()
    {
        try
        {
            Directory.CreateDirectory(destination);
            session = new TorrentSession(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf", "TorrentMetadata"), false);
            manager = await session.AddAsync(source, destination, false, cts.Token);
            state.Text = "Connecting to peers…";
            await manager.StartAsync();
            uiTimer.Start();
            await manager.WaitForMetadataAsync(cts.Token);
            state.Text = "Downloading…";
            while (!cts.IsCancellationRequested && manager.Progress < 99.999)
                await Task.Delay(500, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                complete = true;
                RefreshStats();
                progress.Value = 1000;
                state.Text = "Download complete";
                details.Text = "The files are stored permanently in the selected folder.";
                cancel.Text = "Close";
                cancel.Enabled = true;
                uiTimer.Stop();
            }
        }
        catch (OperationCanceledException) { state.Text = "Download cancelled"; details.Text = "Any partial torrent data has been left in the destination folder."; cancel.Text = "Close"; cancel.Enabled = true; complete = true; }
        catch (Exception ex) { state.Text = "Download failed"; details.Text = ex.Message; cancel.Text = "Close"; cancel.Enabled = true; complete = true; }
    }

    void RefreshStats()
    {
        if (manager == null) return;
        int value = Math.Max(0, Math.Min(1000, (int)Math.Round(manager.Progress * 10)));
        progress.Value = value;
        state.Text = $"{manager.State}  •  {manager.Progress:0.0}%";
        details.Text = $"{FormatRate(manager.Monitor.DownloadRate)} down  •  {manager.Monitor.DataBytesReceived / (1024d * 1024d):0.0} MB received";
    }

    static string FormatRate(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.0} MB/s" : $"{bytes / 1024d:0} KB/s";

    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        closing = true;
        e.Cancel = true;
        cts.Cancel(); uiTimer.Stop();
        if (session != null) await session.DisposeAsync();
        cts.Dispose();
        e.Cancel = false;
        BeginInvoke(Close);
    }
}

internal sealed class StreamForm : Form
{
    static readonly HashSet<string> Playable = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".flv", ".vob" };
    readonly string source, displayTitle;
    readonly VideoView video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly ComboBox fileChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 390, BackColor = Theme.Raised, ForeColor = Theme.Text };
    readonly Label status = Theme.Label("Preparing torrent metadata…");
    readonly Button playPause = Theme.Button("Pause", 90), stop = Theme.Button("Stop", 80);
    readonly TrackBar seek = new() { Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, Width = 280 };
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 500 };
    readonly CancellationTokenSource cts = new();
    readonly string cache;
    TorrentSession? session;
    TorrentManager? manager;
    LibVLC? vlc;
    MediaPlayer? player;
    List<ITorrentManagerFile> playable = new();
    ITorrentManagerFile? selected;
    bool userSeeking, closing;

    public StreamForm(string source, string title)
    {
        this.source = source; displayTitle = title;
        cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf", "StreamingCache", Guid.NewGuid().ToString("N"));
        Theme.Form(this, "Stream locally", new Size(1060, 720)); MinimumSize = new Size(800, 560);
        var top = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Background };
        var accentTop = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Blue };
        var accentLeft = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Red };
        var heading = Theme.Label(title, true); heading.SetBounds(22, 14, 900, 27); heading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var fileLabel = Theme.Label("Video"); fileLabel.SetBounds(22, 51, 42, 26);
        fileChoice.SetBounds(70, 50, 430, 28);
        status.SetBounds(520, 51, 500, 25); status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.AddRange(new Control[] { accentTop, accentLeft, heading, fileLabel, fileChoice, status });

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.Background };
        playPause.SetBounds(22, 12, 90, 34); stop.SetBounds(122, 12, 80, 34); seek.SetBounds(220, 16, 790, 30); seek.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        bottom.Controls.AddRange(new Control[] { playPause, stop, seek });
        Controls.Add(video); Controls.Add(bottom); Controls.Add(top);

        Shown += async (_, _) => await StartAsync();
        FormClosing += OnClosing;
        fileChoice.SelectedIndexChanged += async (_, _) => { if (fileChoice.SelectedIndex >= 0 && fileChoice.SelectedIndex < playable.Count && playable[fileChoice.SelectedIndex] != selected) await PlayFileAsync(playable[fileChoice.SelectedIndex]); };
        playPause.Click += (_, _) => { if (player == null) return; if (player.IsPlaying) { player.Pause(); playPause.Text = "Play"; } else { player.Play(); playPause.Text = "Pause"; } };
        stop.Click += (_, _) => Close();
        seek.MouseDown += (_, _) => userSeeking = true;
        seek.MouseUp += (_, _) => { if (player != null && player.Length > 0) player.Time = (long)(player.Length * (seek.Value / 1000d)); userSeeking = false; };
        uiTimer.Tick += (_, _) => RefreshStats();
    }

    async Task StartAsync()
    {
        try
        {
            Directory.CreateDirectory(cache);
            session = new TorrentSession(Path.Combine(cache, "metadata"), true);
            manager = await session.AddAsync(source, cache, true, cts.Token);
            status.Text = "Connecting • metadata only so far";
            await manager.StartAsync();
            await manager.WaitForMetadataAsync(cts.Token);
            playable = manager.Files.Where(f => Playable.Contains(Path.GetExtension(f.Path))).OrderByDescending(f => f.Length).ToList();
            if (playable.Count == 0) throw new InvalidOperationException("This torrent does not contain a supported video file.");

            foreach (var file in manager.Files) await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);
            fileChoice.BeginUpdate();
            foreach (var file in playable) fileChoice.Items.Add($"{file.Path}  ({FormatSize(file.Length)})");
            fileChoice.EndUpdate();

            Core.Initialize();
            vlc = new LibVLC("--no-video-title-show", "--network-caching=1500");
            player = new MediaPlayer(vlc);
            video.MediaPlayer = player;
            uiTimer.Start();
            fileChoice.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { Close(); }
        catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "VideoShelf streaming", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    async Task PlayFileAsync(ITorrentManagerFile file)
    {
        if (manager == null || session == null || player == null) return;
        selected = file;
        status.Text = "Buffering selected video…";
        foreach (var item in manager.Files) await manager.SetFilePriorityAsync(item, item == file ? Priority.High : Priority.DoNotDownload);
        var httpStream = await manager.StreamProvider.CreateHttpStreamAsync(file, cts.Token);
        string url = session.StreamingUrl(httpStream.RelativeUri);
        using var media = new Media(vlc!, new Uri(url));
        player.Play(media);
        playPause.Text = "Pause";
    }

    void RefreshStats()
    {
        if (manager != null)
            status.Text = $"{manager.State} • {manager.Monitor.DownloadRate / (1024d * 1024d):0.0} MB/s • temporary cache";
        if (!userSeeking && player != null && player.Length > 0)
            seek.Value = Math.Max(0, Math.Min(1000, (int)(player.Time * 1000d / player.Length)));
    }

    static string FormatSize(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024d * 1024d):0.0} GB" : $"{bytes / (1024d * 1024d):0.0} MB";

    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        closing = true; e.Cancel = true; cts.Cancel(); uiTimer.Stop();
        try { player?.Stop(); } catch { }
        video.MediaPlayer = null;
        player?.Dispose(); vlc?.Dispose(); video.Dispose();
        if (session != null) await session.DisposeAsync();
        cts.Dispose();
        try { Directory.Delete(cache, true); } catch { }
        e.Cancel = false; BeginInvoke(Close);
    }
}
