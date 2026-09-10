using System.Drawing.Imaging;

namespace VideoShelf.TransferHost;

internal static class TransferUiCapture
{
    public static void Capture(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        CaptureForm(new DownloadForm("magnet:?xt=urn:btih:0123456789012345678901234567890123456789", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VideoShelf Downloads"), "Re:Zero – Starting Life in Another World (S1) [1080p]", true), Path.Combine(outputDirectory, "VideoShelf-transfer-download.png"));
        CaptureForm(new StreamForm("magnet:?xt=urn:btih:0123456789012345678901234567890123456789", "Re:Zero – Starting Life in Another World (S1) [1080p]", true), Path.Combine(outputDirectory, "VideoShelf-transfer-player.png"));
        CaptureForm(new FileListForm("magnet:?xt=urn:btih:0123456789012345678901234567890123456789", "Re:Zero – Starting Life in Another World (S1) [1080p]", true), Path.Combine(outputDirectory, "VideoShelf-transfer-files.png"));
    }

    static void CaptureForm(Form form, string path)
    {
        using (form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(20, 20);
            form.Show();
            form.PerformLayout();
            Application.DoEvents();
            Thread.Sleep(180);
            Application.DoEvents();
            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(path, ImageFormat.Png);
            form.Hide();
        }
        if (!File.Exists(path) || new FileInfo(path).Length < 5000)
            throw new InvalidOperationException("Transfer UI screenshot was not created correctly: " + Path.GetFileName(path));
    }
}
