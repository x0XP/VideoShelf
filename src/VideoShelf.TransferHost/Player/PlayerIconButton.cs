namespace VideoShelf.TransferHost;

internal enum PlayerIconKind
{
    Play,
    Pause,
    FullScreen,
    ExitFullScreen,
    Subtitles,
    Volume,
    VolumeMuted
}

internal sealed class PlayerIconButton : Control
{
    PlayerIconKind iconKind;
    bool hot;
    bool pressed;

    public PlayerIconKind IconKind
    {
        get => iconKind;
        set
        {
            if (iconKind == value) return;
            iconKind = value;
            Invalidate();
        }
    }

    public PlayerIconButton()
    {
        Size = new Size(42, 34);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable |
                 ControlStyles.StandardClick, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        Color background = !Enabled
            ? Color.FromArgb(10, 20, 28)
            : pressed
                ? Color.FromArgb(16, 39, 59)
                : hot
                    ? Color.FromArgb(20, 48, 72)
                    : Theme.Raised;

        Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using (var brush = new SolidBrush(background)) g.FillRectangle(brush, bounds);
        using (var border = new Pen(hot && Enabled ? Color.FromArgb(63, 94, 119) : Theme.Outline)) g.DrawRectangle(border, bounds);

        Color iconColor = Enabled ? Theme.Text : Theme.Muted;
        using var pen = new Pen(iconColor, 2.2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Square,
            EndCap = System.Drawing.Drawing2D.LineCap.Square
        };
        using var fill = new SolidBrush(iconColor);

        int cx = Width / 2;
        int cy = Height / 2;
        switch (IconKind)
        {
            case PlayerIconKind.Play:
                g.FillPolygon(fill, new[]
                {
                    new Point(cx - 5, cy - 8),
                    new Point(cx - 5, cy + 8),
                    new Point(cx + 8, cy)
                });
                break;

            case PlayerIconKind.Pause:
                g.FillRectangle(fill, cx - 7, cy - 8, 5, 16);
                g.FillRectangle(fill, cx + 2, cy - 8, 5, 16);
                break;

            case PlayerIconKind.FullScreen:
                DrawFullScreen(g, pen, cx, cy, false);
                break;

            case PlayerIconKind.ExitFullScreen:
                DrawFullScreen(g, pen, cx, cy, true);
                break;

            case PlayerIconKind.Subtitles:
                DrawSubtitles(g, pen, cx, cy);
                break;

            case PlayerIconKind.Volume:
                DrawVolume(g, pen, fill, cx, cy, false);
                break;

            case PlayerIconKind.VolumeMuted:
                DrawVolume(g, pen, fill, cx, cy, true);
                break;
        }

        if (Focused)
            ControlPaint.DrawFocusRectangle(g, new Rectangle(4, 4, Math.Max(1, Width - 8), Math.Max(1, Height - 8)), iconColor, background);
    }

    static void DrawFullScreen(Graphics g, Pen pen, int cx, int cy, bool inward)
    {
        int outer = 9;
        int inner = 3;

        if (!inward)
        {
            g.DrawLine(pen, cx - outer, cy - inner, cx - outer, cy - outer);
            g.DrawLine(pen, cx - outer, cy - outer, cx - inner, cy - outer);
            g.DrawLine(pen, cx + inner, cy - outer, cx + outer, cy - outer);
            g.DrawLine(pen, cx + outer, cy - outer, cx + outer, cy - inner);
            g.DrawLine(pen, cx - outer, cy + inner, cx - outer, cy + outer);
            g.DrawLine(pen, cx - outer, cy + outer, cx - inner, cy + outer);
            g.DrawLine(pen, cx + inner, cy + outer, cx + outer, cy + outer);
            g.DrawLine(pen, cx + outer, cy + outer, cx + outer, cy + inner);
        }
        else
        {
            g.DrawLine(pen, cx - outer, cy - inner, cx - inner, cy - inner);
            g.DrawLine(pen, cx - inner, cy - outer, cx - inner, cy - inner);
            g.DrawLine(pen, cx + inner, cy - inner, cx + outer, cy - inner);
            g.DrawLine(pen, cx + inner, cy - outer, cx + inner, cy - inner);
            g.DrawLine(pen, cx - outer, cy + inner, cx - inner, cy + inner);
            g.DrawLine(pen, cx - inner, cy + inner, cx - inner, cy + outer);
            g.DrawLine(pen, cx + inner, cy + inner, cx + outer, cy + inner);
            g.DrawLine(pen, cx + inner, cy + inner, cx + inner, cy + outer);
        }
    }

    static void DrawSubtitles(Graphics g, Pen pen, int cx, int cy)
    {
        Rectangle caption = new(cx - 10, cy - 7, 20, 14);
        g.DrawRectangle(pen, caption);
        using var linePen = new Pen(pen.Color, 1.6f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        g.DrawLine(linePen, cx - 6, cy, cx - 1, cy);
        g.DrawLine(linePen, cx + 2, cy, cx + 6, cy);
        g.DrawLine(linePen, cx - 6, cy + 4, cx, cy + 4);
        g.DrawLine(linePen, cx + 3, cy + 4, cx + 6, cy + 4);
    }

    static void DrawVolume(Graphics g, Pen pen, Brush fill, int cx, int cy, bool muted)
    {
        g.FillRectangle(fill, cx - 10, cy - 3, 4, 6);
        g.FillPolygon(fill, new[]
        {
            new Point(cx - 6, cy - 4),
            new Point(cx, cy - 9),
            new Point(cx, cy + 9),
            new Point(cx - 6, cy + 4)
        });

        if (muted)
        {
            g.DrawLine(pen, cx + 4, cy - 5, cx + 11, cy + 5);
            g.DrawLine(pen, cx + 11, cy - 5, cx + 4, cy + 5);
            return;
        }

        using var arcPen = new Pen(pen.Color, 1.8f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        g.DrawArc(arcPen, cx - 2, cy - 6, 11, 12, -55, 110);
        g.DrawArc(arcPen, cx - 4, cy - 9, 17, 18, -52, 104);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hot = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
        {
            pressed = true;
            Focus();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }
}