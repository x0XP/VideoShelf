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

        // Validate the actual optimized form used by the stream command without showing it.
        // Creating/rendering LibVLC's native VideoView on a headless CI desktop can block, while
        // the WinForms control tree and geometry can be validated safely before Shown starts any I/O.
        using (var optimized = new OptimizedStreamForm(PreviewMagnet, PreviewTitle))
        using (FullscreenPlayerController.Attach(optimized))
            ValidateOptimizedPlayerLayout(optimized);

        CaptureForm(new FileListForm(PreviewMagnet, PreviewTitle, true), Path.Combine(outputDirectory, "VideoShelf-transfer-files.png"));
    }

    static void ValidateOptimizedPlayerLayout(Form form)
    {
        form.PerformLayout();
        foreach (Control child in form.Controls) child.PerformLayout();

        Panel? top = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Top);
        Panel? bottom = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Bottom);
        DarkComboBox? selector = FindControl<DarkComboBox>(form);
        SeekBar? seek = FindControl<SeekBar>(form);
        int iconButtons = CountControls<PlayerIconButton>(form);
        bool hasNativeVideoSurface = FindControlByTypeName(form, "LibVLCSharp.WinForms.VideoView") != null;

        if (top == null || bottom == null || selector == null || seek == null || iconButtons < 2 || !hasNativeVideoSurface)
            throw new InvalidOperationException("The optimized streaming player control tree is incomplete.");
        if (top.Height < 70 || bottom.Height < 55 || selector.Width < 300 || seek.Width < 120)
            throw new InvalidOperationException("The optimized streaming player layout geometry regressed.");
        if (top.Bottom > form.ClientSize.Height || bottom.Top < top.Bottom)
            throw new InvalidOperationException("The optimized streaming player bars overlap or extend outside the form.");
    }

    static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    static Control? FindControlByTypeName(Control root, string fullName)
    {
        if (string.Equals(root.GetType().FullName, fullName, StringComparison.Ordinal)) return root;
        foreach (Control child in root.Controls)
        {
            Control? found = FindControlByTypeName(child, fullName);
            if (found != null) return found;
        }
        return null;
    }

    static int CountControls<T>(Control root) where T : Control
    {
        int count = root is T ? 1 : 0;
        foreach (Control child in root.Controls) count += CountControls<T>(child);
        return count;
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
