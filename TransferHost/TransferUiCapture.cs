using System.Drawing.Imaging;

namespace VideoShelf.TransferHost;

internal static class TransferUiCapture
{
    const string PreviewMagnet = "magnet:?xt=urn:btih:0123456789012345678901234567890123456789";
    const string PreviewTitle = "Re:Zero – Starting Life in Another World (S1) [1080p]";

    public static void Capture(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        CaptureForm(new DownloadForm(PreviewMagnet, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VideoShelf Downloads"), PreviewTitle, true), Path.Combine(outputDirectory, "VideoShelf-transfer-download.png"));

        var stream = new StreamForm(PreviewMagnet, PreviewTitle, true);
        using (FullscreenPlayerController.Attach(stream))
            CaptureForm(stream, Path.Combine(outputDirectory, "VideoShelf-transfer-player.png"));

        // Render the actual optimized player used by the stream command, but never show it.
        // OptimizedStreamForm starts network/torrent work from Shown, so creating its handle and
        // drawing it off-screen gives us a real runtime-layout regression check without media I/O.
        var optimized = new OptimizedStreamForm(PreviewMagnet, PreviewTitle);
        using (FullscreenPlayerController.Attach(optimized))
            CaptureUnshownForm(optimized, Path.Combine(outputDirectory, "VideoShelf-transfer-player-runtime.png"));

        CaptureForm(new FileListForm(PreviewMagnet, PreviewTitle, true), Path.Combine(outputDirectory, "VideoShelf-transfer-files.png"));
    }

    static void CaptureForm(Form form, string path)
    {
        using (form)
        using (var host = PrepareHost(form))
        {
            form.Show();
            PrepareLayout(form);
            Application.DoEvents();
            Thread.Sleep(180);
            Application.DoEvents();
            SaveBitmap(form, path);
            form.Hide();
            host.Controls.Remove(form);
        }
        VerifyCapture(path);
    }

    static void CaptureUnshownForm(Form form, string path)
    {
        using (form)
        using (var host = PrepareHost(form))
        {
            CreateControlTree(form);
            PrepareLayout(form);
            SaveBitmap(form, path);
            host.Controls.Remove(form);
        }
        VerifyCapture(path);
    }

    static Panel PrepareHost(Form form)
    {
        Size intended = form.ClientSize;
        form.MinimumSize = Size.Empty;
        form.MaximumSize = Size.Empty;
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = Point.Empty;
        form.ClientSize = intended;

        var host = new Panel { Size = intended, BackColor = Theme.Background };
        host.CreateControl();
        host.Controls.Add(form);
        return host;
    }

    static void CreateControlTree(Control root)
    {
        root.CreateControl();
        foreach (Control child in root.Controls)
            CreateControlTree(child);
    }

    static void PrepareLayout(Form form)
    {
        form.PerformLayout();
        foreach (Control child in form.Controls) child.PerformLayout();
    }

    static void SaveBitmap(Form form, string path)
    {
        Size intended = form.ClientSize;
        using var bitmap = new Bitmap(intended.Width, intended.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap)) g.Clear(Theme.Background);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, intended));
        bitmap.Save(path, ImageFormat.Png);
    }

    static void VerifyCapture(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < 5000)
            throw new InvalidOperationException("Transfer UI screenshot was not created correctly: " + Path.GetFileName(path));
    }
}
