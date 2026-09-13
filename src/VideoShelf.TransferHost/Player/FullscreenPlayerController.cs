namespace VideoShelf.TransferHost;

internal sealed class FullscreenPlayerController : IDisposable
{
    static readonly TimeSpan FullScreenControlsIdleTimeout = TimeSpan.FromSeconds(2.5);
    const int ControlsAnimationDurationMs = 180;

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
    readonly System.Windows.Forms.Timer pointerIdleTimer = new() { Interval = 100 };
    readonly System.Windows.Forms.Timer controlsAnimationTimer = new() { Interval = 15 };
    FormBorderStyle savedBorderStyle;
    FormWindowState savedWindowState;
    Rectangle savedBounds;
    Point lastPointerPosition;
    DateTime lastPointerActivityUtc;
    long controlsAnimationStarted;
    int expandedBottomBarHeight;
    int controlsAnimationStartHeight;
    int controlsAnimationTargetHeight;
    bool savedTopMost;
    bool isFullScreen;
    bool controlsRequestedVisible = true;
    bool disposed;

    FullscreenPlayerController(Form form)
    {
        this.form = form;
        topBar = form.Controls.OfType<Panel>().First(p => p.Dock == DockStyle.Top);
        bottomBar = form.Controls.OfType<Panel>().First(p => p.Dock == DockStyle.Bottom);
        expandedBottomBarHeight = Math.Max(1, bottomBar.Height);
        seek = bottomBar.Controls.OfType<SeekBar>().First();
        timeLabel = bottomBar.Controls.OfType<Label>().First();

        sourcePlayPause = bottomBar.Controls.OfType<Button>()
            .FirstOrDefault(b => b.Text.Equals("Play", StringComparison.OrdinalIgnoreCase) || b.Text.Equals("Pause", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Streaming player play/pause control was not found.");
        sourceStop = bottomBar.Controls.OfType<Button>()
            .FirstOrDefault(b => b.Text.Equals("Stop", StringComparison.OrdinalIgnoreCase));

        // Keep the source button logically visible so WinForms PerformClick can dispatch its Click event.
        // It is moved outside the viewport because the icon button below is the player-facing control.
        sourcePlayPause.TabStop = false;
        sourcePlayPause.SetBounds(-10000, -10000, 1, 1);
        sourcePlayPause.Visible = true;
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
        pointerIdleTimer.Tick += OnPointerIdleTimerTick;
        controlsAnimationTimer.Tick += OnControlsAnimationTick;
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
        expandedBottomBarHeight = Math.Max(expandedBottomBarHeight, bottomBar.Height);

        Rectangle screen = Screen.FromControl(form).Bounds;
        form.SuspendLayout();
        form.WindowState = FormWindowState.Normal;
        form.FormBorderStyle = FormBorderStyle.None;
        form.TopMost = true;
        form.Bounds = screen;
        topBar.Visible = false;
        bottomBar.Height = expandedBottomBarHeight;
        bottomBar.Visible = true;
        controlsRequestedVisible = true;
        fullScreen.IconKind = PlayerIconKind.ExitFullScreen;
        fullScreen.AccessibleName = "Exit full screen";
        toolTip.SetToolTip(fullScreen, "Exit full screen");
        form.ResumeLayout(true);
        isFullScreen = true;
        ResetPointerIdleTracking();
        pointerIdleTimer.Start();
        RelayoutControls();
    }

    void ExitFullScreen()
    {
        if (!isFullScreen || disposed) return;
        pointerIdleTimer.Stop();
        controlsAnimationTimer.Stop();
        form.SuspendLayout();
        topBar.Visible = true;
        bottomBar.Visible = true;
        bottomBar.Height = expandedBottomBarHeight;
        controlsRequestedVisible = true;
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

    void ResetPointerIdleTracking()
    {
        lastPointerPosition = Cursor.Position;
        lastPointerActivityUtc = DateTime.UtcNow;
    }

    void OnPointerIdleTimerTick(object? sender, EventArgs e)
    {
        if (!isFullScreen || disposed) return;

        Point pointerPosition = Cursor.Position;
        if (pointerPosition != lastPointerPosition)
        {
            lastPointerPosition = pointerPosition;
            lastPointerActivityUtc = DateTime.UtcNow;
            BeginControlsAnimation(true);
            return;
        }

        if (controlsRequestedVisible && DateTime.UtcNow - lastPointerActivityUtc >= FullScreenControlsIdleTimeout)
            BeginControlsAnimation(false);
    }

    void BeginControlsAnimation(bool show)
    {
        if (!isFullScreen || disposed) return;
        int target = show ? expandedBottomBarHeight : 0;
        controlsRequestedVisible = show;
        if (controlsAnimationTimer.Enabled && controlsAnimationTargetHeight == target) return;
        if (show && !bottomBar.Visible)
        {
            bottomBar.Height = 0;
            bottomBar.Visible = true;
        }

        if (bottomBar.Height == target)
        {
            controlsAnimationTimer.Stop();
            if (!show) bottomBar.Visible = false;
            return;
        }

        controlsAnimationStartHeight = bottomBar.Height;
        controlsAnimationTargetHeight = target;
        controlsAnimationStarted = Environment.TickCount64;
        controlsAnimationTimer.Start();
    }

    void OnControlsAnimationTick(object? sender, EventArgs e)
    {
        if (!isFullScreen || disposed)
        {
            controlsAnimationTimer.Stop();
            return;
        }

        double t = Math.Min(1d, (Environment.TickCount64 - controlsAnimationStarted) / (double)ControlsAnimationDurationMs);
        double eased = t * t * (3d - 2d * t);
        int height = (int)Math.Round(controlsAnimationStartHeight + (controlsAnimationTargetHeight - controlsAnimationStartHeight) * eased);
        bottomBar.Height = Math.Max(0, height);
        form.PerformLayout();

        if (t >= 1d)
        {
            controlsAnimationTimer.Stop();
            bottomBar.Height = controlsAnimationTargetHeight;
            if (controlsAnimationTargetHeight == 0) bottomBar.Visible = false;
            form.PerformLayout();
        }
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
        pointerIdleTimer.Stop();
        controlsAnimationTimer.Stop();
        pointerIdleTimer.Tick -= OnPointerIdleTimerTick;
        controlsAnimationTimer.Tick -= OnControlsAnimationTick;
        form.KeyDown -= OnKeyDown;
        form.Resize -= OnResize;
        form.FormClosed -= OnFormClosed;
        playPause.Click -= OnPlayPauseClick;
        fullScreen.Click -= OnFullScreenClick;
        sourcePlayPause.TextChanged -= OnSourcePlayPauseChanged;
        sourcePlayPause.EnabledChanged -= OnSourcePlayPauseChanged;
        UnhookDoubleClick(form);
        pointerIdleTimer.Dispose();
        controlsAnimationTimer.Dispose();
        toolTip.Dispose();
        if (!playPause.IsDisposed) playPause.Dispose();
        if (!fullScreen.IsDisposed) fullScreen.Dispose();
    }
}
