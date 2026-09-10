using System.Runtime.InteropServices;

namespace VideoShelf.TransferHost;

internal static class NativeTheme
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    public static void Apply(Form form)
    {
        void ApplyNow()
        {
            try
            {
                int enabled = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch { }
        }
        if (form.IsHandleCreated) ApplyNow();
        form.HandleCreated += (_, _) => ApplyNow();
    }

    public static void Apply(Control control)
    {
        try
        {
            if (!control.IsHandleCreated) control.CreateControl();
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }
        catch { }
    }
}

internal sealed class AccentProgressBar : Control
{
    int value;
    public int Value
    {
        get => value;
        set { this.value = Math.Max(0, Math.Min(1000, value)); Invalidate(); }
    }

    public AccentProgressBar()
    {
        Height = 18;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var track = new Rectangle(0, 3, Math.Max(1, Width - 1), Math.Max(7, Height - 7));
        using (var b = new SolidBrush(Color.FromArgb(13, 27, 38))) g.FillRectangle(b, track);
        using (var p = new Pen(Theme.Outline)) g.DrawRectangle(p, track);
        int fill = (int)Math.Round((track.Width - 2) * (value / 1000d));
        if (fill > 0)
        {
            using var b = new SolidBrush(Theme.Blue);
            g.FillRectangle(b, track.Left + 1, track.Top + 1, fill, Math.Max(1, track.Height - 1));
        }
    }
}

internal sealed class SeekBar : Control
{
    int value;
    bool dragging;
    public event EventHandler? ValueCommitted;
    public bool IsDragging => dragging;

    public int Value
    {
        get => value;
        set { this.value = Math.Max(0, Math.Min(1000, value)); Invalidate(); }
    }

    public SeekBar()
    {
        Height = 30;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }

    void SetFromX(int x)
    {
        int left = 8, width = Math.Max(1, Width - 16);
        Value = (int)Math.Round(Math.Max(0, Math.Min(width, x - left)) * 1000d / width);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        dragging = true; Capture = true; SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!dragging) return;
        dragging = false; Capture = false; SetFromX(e.X); ValueCommitted?.Invoke(this, EventArgs.Empty);
        if (FindForm() is Form form && form.ActiveControl == this) form.ActiveControl = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Down) { Value -= 20; ValueCommitted?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (e.KeyCode is Keys.Right or Keys.Up) { Value += 20; ValueCommitted?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (e.KeyCode == Keys.Home) { Value = 0; ValueCommitted?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (e.KeyCode == Keys.End) { Value = 1000; ValueCommitted?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        int y = Height / 2;
        using (var track = new Pen(Color.FromArgb(49, 71, 91), 4)) g.DrawLine(track, 8, y, Math.Max(8, Width - 8), y);
        int x = 8 + (int)Math.Round(Math.Max(1, Width - 16) * (value / 1000d));
        using (var fill = new Pen(Theme.Blue, 4)) g.DrawLine(fill, 8, y, x, y);
        using (var b = new SolidBrush(Enabled ? Theme.Text : Theme.Muted)) g.FillEllipse(b, x - 6, y - 6, 12, 12);
        if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 3, Math.Max(1, Width - 4), Math.Max(1, Height - 6)));
    }
}

internal sealed class DarkComboBox : ComboBox
{
    public string EmptyText { get; set; } = "Resolving torrent videos…";

    public DarkComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 27;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);
        NativeTheme.Apply(this);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(selected ? Color.FromArgb(24, 64, 105) : Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
        string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : EmptyText;
        TextRenderer.DrawText(e.Graphics, text, Font,
            new Rectangle(e.Bounds.X + 9, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 18), e.Bounds.Height),
            Enabled ? (selected ? Color.White : Theme.Text) : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}

internal sealed class DarkListView : ListView
{
    readonly Font headerFont = new("Segoe UI", 8.5f, FontStyle.Bold);
    public DarkListView()
    {
        View = View.Details;
        FullRowSelect = true;
        HideSelection = false;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        OwnerDraw = true;
        Font = new Font("Segoe UI", 9.2f);
        NativeTheme.Apply(this);
        DrawColumnHeader += (_, e) =>
        {
            using (var b = new SolidBrush(Color.FromArgb(12, 24, 34))) e.Graphics.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Color.FromArgb(39, 58, 73))) e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont,
                new Rectangle(e.Bounds.X + 9, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 12), e.Bounds.Height),
                Color.FromArgb(184, 202, 219), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
        DrawItem += (_, _) => { };
        DrawSubItem += (_, e) =>
        {
            bool selected = e.Item.Selected;
            using (var b = new SolidBrush(selected ? Color.FromArgb(24, 64, 105) : Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 10), e.Bounds.Height),
                selected ? Color.White : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) headerFont.Dispose();
        base.Dispose(disposing);
    }
}
