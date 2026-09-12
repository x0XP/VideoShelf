namespace VideoShelf.TransferHost;

internal sealed class FullscreenPlayerController : IDisposable
{
    readonly Form form;
    readonly Panel topBar;
    readonly Panel bottomBar;
    readonly SeekBar seek;
    readonly Label timeLabel;
    readonly Button sourcePlayPause;
    readonly Button? sourceStop;
    readonly PlayerIconButton playPause;
    readonly PlayerIconButton fullScreen;
    readonly ToolTip toolTip = new();
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

        sourcePlayPause = bottomBar.Controls.OfType<Button>()
            .FirstOrDefault(b => b.Text.Equals("Play", StringComparison.OrdinalIgnoreCase) || b.Text.Equals("Pause", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Streaming player play/pause control was not found.");
        sourceStop = bottomBar.Controls.OfType<Button>()
            .FirstOrDefault(b => b.Text.Equals("Stop", StringComparison.OrdinalIgnoreCase));

        sourcePlayPause.Visible = false;
        sourcePlayPause.TabStop = false;
        if (sourceStop != null)
        {
            sourceStop.Visible = false;
            sourceStop.TabStop = false;
        }

        playPause = new PlayerIconButton();
        fullScreen = new PlayerIconButton { IconKind = PlayerIconKind.FullScreen };
        playPause.AccessibleName = "Play or pause";
        fullScreen.AccessibleName = "Toggle full screen";
        bottomBar.Controls.Add(playPause);
        bottomBar.Controls.Add(fullScreen);

        UpdatePlayPauseIcon();
        toolTip.SetToolTip(playPause, sourcePlayPause.Text.Equals("Pause", StringComparison.OrdinalIgnoreCase) ? "Pause" : "Play");
        toolTip.SetToolTip(fullScreen, "Full screen");

        form.KeyPreview = true;
        form.KeyDown += OnKeyDown;
        form.Resize += OnResize;
        form.FormClosed += OnFormClosed;
        playPause.Click += OnPlayPauseClick;
        fullScreen.Click += OnFullScreenClick;
        sourcePlayPause.TextChanged += OnSourcePlayPauseChanged;
        sourcePlayPause.EnabledChanged += OnSourcePlayPauseChanged;
        HookDoubleClick(form);
        RelayoutControls();
    }

    public static FullscreenPlayerController Attach(Form form)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));
        return new FullscreenPlayerController(form);
    }

    void OnPlayPauseClick(object? sender, EventArgs e)
    {
        if (!sourcePlayPause.Enabled) return;
        sourcePlayPause.PerformClick();
        UpdatePlayPauseIcon();
    }

    void OnSourcePlayPauseChanged(object? sender, EventArgs e) => UpdatePlayPauseIcon();

    void UpdatePlayPauseIcon()
    {
        bool pausedAction = sourcePlayPause.Text.Equals("Pause", StringComparison.OrdinalIgnoreCase);
        playPause.IconKind = pausedAction ? PlayerIconKind.Pause : PlayerIconKind.Play;
        playPause.Enabled = sourcePlayPause.Enabled;
        playPause.AccessibleName = pausedAction ? "Pause" : "Play";
        toolTip.SetToolTip(playPause, pausedAction ? "Pause" : "Play");
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

    void UnhookDoubleClick(Control root)
    {
        if (root != bottomBar && root != topBar)
            root.DoubleClick -= OnVideoDoubleClick;
        foreach (Control child in root.Controls)
            UnhookDoubleClick(child);
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
        fullScreen.IconKind = PlayerIconKind.ExitFullScreen;
        fullScreen.AccessibleName = "Exit full screen";
        toolTip.SetToolTip(fullScreen, "Exit full screen");
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
        fullScreen.IconKind = PlayerIconKind.FullScreen;
        fullScreen.AccessibleName = "Full screen";
        toolTip.SetToolTip(fullScreen, "Full screen");
        form.ResumeLayout(true);
        isFullScreen = false;
        RelayoutControls();
    }

    void OnResize(object? sender, EventArgs e) => RelayoutControls();

    void RelayoutControls()
    {
        if (disposed || bottomBar.IsDisposed) return;

        const int iconWidth = 42;
        const int timeWidth = 150;
        int fullX = Math.Max(100, form.ClientSize.Width - 22 - iconWidth);
        int timeX = Math.Max(230, fullX - 12 - timeWidth);
        int seekX = 76;
        int seekWidth = Math.Max(130, timeX - seekX - 12);

        playPause.SetBounds(22, 16, iconWidth, 34);
        seek.SetBounds(seekX, 18, seekWidth, 30);
        timeLabel.SetBounds(timeX, 22, timeWidth, 22);
        fullScreen.SetBounds(fullX, 16, iconWidth, 34);

        playPause.BringToFront();
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
        playPause.Click -= OnPlayPauseClick;
        fullScreen.Click -= OnFullScreenClick;
        sourcePlayPause.TextChanged -= OnSourcePlayPauseChanged;
        sourcePlayPause.EnabledChanged -= OnSourcePlayPauseChanged;
        UnhookDoubleClick(form);
        toolTip.Dispose();
        if (!playPause.IsDisposed) playPause.Dispose();
        if (!fullScreen.IsDisposed) fullScreen.Dispose();
    }
}
