using System.Reflection;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal sealed class FileListForm : Form
{
    readonly string source;
    readonly Label state = Theme.Label("Retrieving torrent metadata…");
    readonly DarkListView files = new();
    readonly Button close = Theme.Button("Close", 100);
    readonly CancellationTokenSource cts = new();
    readonly string cache;
    readonly bool previewOnly;
    TorrentSession? session;
    bool closing;

    public FileListForm(string source, string title, bool previewOnly = false)
    {
        this.source = source;
        this.previewOnly = previewOnly;
        cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf", "MetadataPreview", Guid.NewGuid().ToString("N"));

        Theme.Form(this, "View files", new Size(820, 570));
        MinimumSize = new Size(680, 460);

        var accentTop = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Blue };
        var accentLeft = new Panel { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Red };
        var heading = Theme.Label("TORRENT FILES", true); heading.SetBounds(25, 22, 740, 26);
        var name = Theme.Label(title, true); name.SetBounds(25, 55, 750, 27); name.AutoEllipsis = true;
        state.SetBounds(25, 87, 750, 24); state.AutoEllipsis = true;

        files.SetBounds(25, 124, 770, 374);
        files.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        files.Columns.Add("FILE", 540);
        files.Columns.Add("TYPE", 80);
        files.Columns.Add("SIZE", 125);

        close.SetBounds(695, 515, 100, 34);
        close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        Controls.AddRange(new Control[] { accentTop, accentLeft, heading, name, state, files, close });
        close.Click += (_, _) => Close();
        if (!previewOnly) Shown += async (_, _) => await LoadMetadataAsync();
        else PreparePreview();
        FormClosing += OnClosing;
        Resize += (_, _) => FitColumns();
        FitColumns();
    }

    void PreparePreview()
    {
        state.Text = "6 files • metadata only • no media payload downloaded";
        files.Items.Add(new ListViewItem(new[] { "Re.Zero.S01E01.1080p.mkv", "MKV", "1.4 GB" }));
        files.Items.Add(new ListViewItem(new[] { "Re.Zero.S01E02.1080p.mkv", "MKV", "1.3 GB" }));
        files.Items.Add(new ListViewItem(new[] { "Re.Zero.S01E03.1080p.mkv", "MKV", "1.4 GB" }));
        files.Items.Add(new ListViewItem(new[] { "Subs/English.ass", "ASS", "82 KB" }));
        files.Items.Add(new ListViewItem(new[] { "Subs/Signs.ass", "ASS", "31 KB" }));
        files.Items.Add(new ListViewItem(new[] { "cover.jpg", "JPG", "418 KB" }));
    }

    void FitColumns()
    {
        if (files.Columns.Count != 3 || files.ClientSize.Width <= 0) return;
        int type = 82, size = 125;
        files.Columns[1].Width = type;
        files.Columns[2].Width = size;
        files.Columns[0].Width = Math.Max(220, files.ClientSize.Width - type - size);
        int visibleRows = Math.Max(1, (Math.Max(0, files.ClientSize.Height - 25)) / 22);
        files.Scrollable = files.Items.Count > visibleRows;
        NativeTheme.Apply(files);
    }

    async Task LoadMetadataAsync()
    {
        try
        {
            Directory.CreateDirectory(cache);
            session = new TorrentSession(Path.Combine(cache, "metadata"), false);
            TorrentManager manager = await session.AddAsync(source, cache, false, cts.Token);

            if (!manager.HasMetadata)
            {
                state.Text = "Connecting for torrent metadata only…";
                MethodInfo? metadataStart = typeof(TorrentManager)
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "StartAsync" &&
                                         m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType == typeof(bool));
                if (metadataStart == null)
                    throw new InvalidOperationException("The bundled torrent engine does not expose metadata-only startup.");

                object? invoked = metadataStart.Invoke(manager, new object[] { true });
                if (invoked is Task startTask) await startTask;
                else throw new InvalidOperationException("Could not start the metadata-only torrent session.");

                await manager.WaitForMetadataAsync(cts.Token);
            }

            try { await manager.StopAsync(); } catch { }

            files.BeginUpdate();
            files.Items.Clear();
            foreach (var file in manager.Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
            {
                string ext = Path.GetExtension(file.Path).TrimStart('.').ToUpperInvariant();
                if (ext.Length == 0) ext = "FILE";
                files.Items.Add(new ListViewItem(new[] { file.Path, ext, FormatSize(file.Length) }));
            }
            files.EndUpdate();
            FitColumns();

            state.Text = $"{files.Items.Count} files • metadata only • no media payload downloaded";
        }
        catch (OperationCanceledException)
        {
            state.Text = "Metadata lookup cancelled.";
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            state.Text = ex.InnerException.Message;
            MessageBox.Show(this, ex.InnerException.Message, "VideoShelf torrent files", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            state.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "VideoShelf torrent files", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024d * 1024d):0.0} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / (1024d * 1024d):0.0} MB";
        return $"{bytes / 1024d:0} KB";
    }

    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        closing = true;
        e.Cancel = true;
        cts.Cancel();
        if (session != null) await session.DisposeAsync();
        cts.Dispose();
        try { Directory.Delete(cache, true); } catch { }
        e.Cancel = false;
        BeginInvoke(Close);
    }
}
