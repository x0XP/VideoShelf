using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using LibVLCSharp.Shared;
using MonoTorrent;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal static class Program
{
    internal static void Run(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                TransferRuntime.SelfTest();
                return;
            }
            if (args.Length >= 1 && args[0].Equals("--screenshot-ui", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "TransferHost-ui");
                TransferUiCapture.Capture(output);
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

            throw new ArgumentException("Use download --source <magnet-or-torrent-url> --destination <folder> --title <title>, stream --source <magnet-or-torrent-url> --title <title>, files --source <magnet-or-torrent-url> --title <title>, --screenshot-ui <folder>, or --self-test.");
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
    public static readonly Color Panel = Color.FromArgb(8, 17, 25);
    public static readonly Color Raised = Color.FromArgb(14, 27, 38);
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
        NativeTheme.Apply(form);
    }

    public static Button Button(string text, int width = 130)
    {
        var b = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Raised, ForeColor = Text, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.2f) };
        b.FlatAppearance.BorderColor = Outline;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 48, 72);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(16, 39, 59);
        NativeTheme.Apply(b);
        return b;
    }

    public static Label Label(string text, bool strong = false)
        => new() { Text = text, AutoSize = false, ForeColor = strong ? Color.White : Muted, Font = new Font("Segoe UI", strong ? 11f : 9.5f, strong ? FontStyle.Bold : FontStyle.Regular) };
}

internal sealed class TorrentSession : IAsyncDisposable
{
    const int MaxMetadataBytes = 16 * 1024 * 1024;
    static readonly HttpClient Http = CreateHttp();
    static readonly MethodInfo? MetadataOnlyStart = typeof(TorrentManager)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .FirstOrDefault(m => m.Name == "StartAsync" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));
    readonly string cacheRoot;
    string? metadataFile;

    public ClientEngine Engine { get; }
    public TorrentManager? Manager { get; private set; }

    public TorrentSession(string cacheRoot)
    {
        this.cacheRoot = cacheRoot;
        Directory.CreateDirectory(cacheRoot);
        var builder = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cacheRoot
        };
        Engine = new ClientEngine(builder.ToSettings());
    }

    static HttpClient CreateHttp()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
        return client;
    }

    public async Task<TorrentManager> AddAsync(string source, string destination, CancellationToken token)
    {
        if (MagnetLink.TryParse(source, out MagnetLink? magnet) && magnet != null)
        {
            Manager = await Engine.AddAsync(magnet, destination);
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

        Manager = await Engine.AddAsync(metadataFile, destination);
        return Manager;
    }

    public static async Task StartMetadataOnlyAsync(TorrentManager manager)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (MetadataOnlyStart == null)
            throw new MissingMethodException("MonoTorrent metadata-only startup API is unavailable.");
        object? invoked;
        try { invoked = MetadataOnlyStart.Invoke(manager, new object[] { true }); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        if (invoked is Task task) await task;
        else throw new InvalidOperationException("MonoTorrent metadata startup did not return a task.");
    }

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
        if (MetadataOnlyStart == null)
            throw new InvalidOperationException("MonoTorrent metadata-only startup API changed; torrent file inspection cannot safely retrieve magnet metadata.");
        string root = Path.Combine(Path.GetTempPath(), "VideoShelf-transfer-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var normalSession = new TorrentSession(Path.Combine(root, "normal"));
            normalSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Core.Initialize();
            using var vlc = new LibVLC("--no-video-title-show", "--network-caching=1800");
            using var player = new MediaPlayer(vlc);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

internal sealed class DownloadForm : Form
{
    readonly string source, destination;
    readonly Label state = Theme.Label("Preparing torrent metadata…"), details = Theme.Label("No media is transferred until this explicit download action."), destinationLabel = Theme.Label("");
    readonly AccentProgressBar progress = new();
    readonly Button cancel = Theme.Button("Cancel", 105), openFolder = Theme.Button("Open folder", 120);
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 500 };
    readonly CancellationTokenSource cts = new();
    readonly bool previewOnly;
    TorrentSession? session;
    TorrentManager? manager;
    bool complete, closing;

    public DownloadForm(string source, string destination, string title, bool previewOnly = false)
    {
        this.source = source; this.destination = destination; this.previewOnly = previewOnly;
        Theme.Form(this, "Download locally", new Size(700, 300));
        var accentTop = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Blue };
        var accentLeft = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Red };
        var heading = Theme.Label("DOWNLOAD LOCALLY", true); heading.SetBounds(28, 26, 620, 28);
        var name = Theme.Label(title, true); name.SetBounds(28, 62, 640, 28); name.AutoEllipsis = true;
        destinationLabel.Text = destination; destinationLabel.SetBounds(28, 96, 640, 24); destinationLabel.AutoEllipsis = true;
        progress.SetBounds(28, 137, 640, 18);
        state.SetBounds(28, 170, 640, 24);
        details.SetBounds(28, 198, 640, 24); details.AutoEllipsis = true;
        openFolder.SetBounds(438, 244, 120, 34); openFolder.Enabled = previewOnly || Directory.Exists(destination);
        cancel.SetBounds(568, 244, 100, 34);
        Controls.AddRange(new Control[] { accentTop, accentLeft, heading, name, destinationLabel, progress, state, details, openFolder, cancel });
        if (!previewOnly) Shown += async (_, _) => await StartAsync();
        else { progress.Value = 420; state.Text = "Downloading  •  42.0%"; details.Text = "5.8 MB/s down  •  3,612 MB received"; cancel.Text = "Cancel"; }
        FormClosing += OnClosing;
        cancel.Click += (_, _) => { if (complete || previewOnly) Close(); else { cts.Cancel(); cancel.Enabled = false; state.Text = "Stopping…"; } };
        openFolder.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true }); } catch { } };
        uiTimer.Tick += (_, _) => RefreshStats();
    }

    async Task StartAsync()
    {
        try
        {
            Directory.CreateDirectory(destination);
            openFolder.Enabled = true;
            var transferSession = new TorrentSession(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf", "TorrentMetadata"));
            session = transferSession;
            var torrentManager = await transferSession.AddAsync(source, destination, cts.Token);
            manager = torrentManager;
            state.Text = "Connecting to peers…";
            await torrentManager.StartAsync();
            uiTimer.Start();
            await torrentManager.WaitForMetadataAsync(cts.Token);
            state.Text = "Downloading…";
            while (!cts.IsCancellationRequested && torrentManager.Progress < 99.999)
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
        progress.Value = Math.Max(0, Math.Min(1000, (int)Math.Round(manager.Progress * 10)));
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
