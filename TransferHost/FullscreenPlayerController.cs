namespace VideoShelf.TransferHost;

internal sealed class FullscreenPlayerController : IDisposable
{
    readonly Form form;
    readonly Panel topBar;
    readonly Panel bottomBar;
    readonly SeekBar seek;
    readonly Label timeLabel;
    readonly Button fullScreen;
    FormBorderStyle savedBorderStyle;
    FormWindowState savedWindowState;
    Rectangle savedBounds;
    bool savedTopMost;
    bool isFullScreen;
    bool disposed;

    FullscreenPlayerController(Form form)
    {
        this.form = form;
        topBar = form.Controls.OfType<Panel>().First(p => p.Dock == DockStyle.Top);
        bottomBar = form.Controls.OfType<Panel>().First(p => p.Dock == DockStyle.Bottom);
        seek = bottomBar.Controls.OfType<SeekBar>().First();
        timeLabel = bottomBar.Controls.OfType<Label>().First();
        fullScreen = Theme.Button("Full screen", 110);
        fullScreen.AccessibleName = "Toggle full screen";
        fullScreen.TabStop = true;
        bottomBar.Controls.Add(fullScreen);

        form.KeyPreview = true;
        form.KeyDown += OnKeyDown;
        form.Resize += OnResize;
        form.FormClosed += OnFormClosed;
        fullScreen.Click += OnFullScreenClick;
        HookDoubleClick(form);
        RelayoutControls();
    }

    public static FullscreenPlayerController Attach(Form form)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));
        return new FullscreenPlayerController(form);
    }

    void OnFullScreenClick(object? sender, EventArgs e) => ToggleFullScreen();

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape && isFullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    void HookDoubleClick(Control root)
    {
        if (root != bottomBar && root != topBar)
            root.DoubleClick += OnVideoDoubleClick;
        foreach (Control child in root.Controls)
            HookDoubleClick(child);
    }

    void OnVideoDoubleClick(object? sender, EventArgs e)
    {
        if (sender is Control control && (control == bottomBar || control == topBar || bottomBar.Contains(control) || topBar.Contains(control)))
            return;
        ToggleFullScreen();
    }

    void ToggleFullScreen()
    {
        if (isFullScreen) ExitFullScreen();
        else EnterFullScreen();
    }

    void EnterFullScreen()
    {
        if (isFullScreen || disposed) return;
        savedBorderStyle = form.FormBorderStyle;
        savedWindowState = form.WindowState;
        savedBounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        savedTopMost = form.TopMost;

        Rectangle screen = Screen.FromControl(form).Bounds;
        form.SuspendLayout();
        form.WindowState = FormWindowState.Normal;
        form.FormBorderStyle = FormBorderStyle.None;
        form.TopMost = true;
        form.Bounds = screen;
        topBar.Visible = false;
        bottomBar.Visible = true;
        fullScreen.Text = "Exit full screen";
        form.ResumeLayout(true);
        isFullScreen = true;
        RelayoutControls();
    }

    void ExitFullScreen()
    {
        if (!isFullScreen || disposed) return;
        form.SuspendLayout();
        topBar.Visible = true;
        form.TopMost = savedTopMost;
        form.FormBorderStyle = savedBorderStyle;
        form.WindowState = FormWindowState.Normal;
        if (!savedBounds.IsEmpty) form.Bounds = savedBounds;
        form.WindowState = savedWindowState;
        fullScreen.Text = "Full screen";
        form.ResumeLayout(true);
        isFullScreen = false;
        RelayoutControls();
    }

    void OnResize(object? sender, EventArgs e) => RelayoutControls();

    void RelayoutControls()
    {
        if (disposed || bottomBar.IsDisposed) return;
        int timeWidth = 150;
        int timeX = Math.Max(525, form.ClientSize.Width - 22 - timeWidth);
        int fullX = 212;
        fullScreen.SetBounds(fullX, 16, 110, 34);
        seek.SetBounds(338, 18, Math.Max(170, timeX - 350), 30);
        timeLabel.SetBounds(timeX, 22, timeWidth, 22);
        fullScreen.BringToFront();
    }

    void OnFormClosed(object? sender, FormClosedEventArgs e) => Dispose();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        form.KeyDown -= OnKeyDown;
        form.Resize -= OnResize;
        form.FormClosed -= OnFormClosed;
        fullScreen.Click -= OnFullScreenClick;
        if (!fullScreen.IsDisposed) fullScreen.Dispose();
    }
}
