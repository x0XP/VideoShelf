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

        // Capture the actual optimized player layout. Fixture mode deliberately does not create
        // LibVLC's native VideoView handle or start a torrent, so CI validates the production
        // controls/layout without network traffic, media payload or headless native-video hangs.
        var stream = new OptimizedStreamForm(PreviewMagnet, PreviewTitle, true);
        using (FullscreenPlayerController.Attach(stream))
            CaptureForm(stream, Path.Combine(outputDirectory, "VideoShelf-transfer-player.png"));

        CaptureForm(new FileListForm(PreviewMagnet, PreviewTitle, true), Path.Combine(outputDirectory, "VideoShelf-transfer-files.png"));
    }

    static void CaptureForm(Form form, string path)
    {
        using (form)
        using (var host = new Panel())
        {
            Size intended = form.ClientSize;
            form.MinimumSize = Size.Empty;
            form.MaximumSize = Size.Empty;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            form.ClientSize = intended;
            host.Size = intended;
            host.BackColor = Theme.Background;
            host.CreateControl();
            host.Controls.Add(form);
            form.Show();
            form.PerformLayout();
            foreach (Control child in form.Controls) child.PerformLayout();
            Application.DoEvents();
            Thread.Sleep(180);
            Application.DoEvents();
            using var bitmap = new Bitmap(intended.Width, intended.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap)) g.Clear(Theme.Background);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, intended));
            bitmap.Save(path, ImageFormat.Png);
            form.Hide();
            host.Controls.Remove(form);
        }
        if (!File.Exists(path) || new FileInfo(path).Length < 5000)
            throw new InvalidOperationException("Transfer UI screenshot was not created correctly: " + Path.GetFileName(path));
    }
}
