using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VideoShelf
{
    sealed class MockupActionButton : Control
    {
        string subtitleText = "";
        public string TitleText = "";
        public string Glyph = "";
        public Color Accent = XdolfTheme.AccentBlue;
        bool hot, pressed;

        public string SubtitleText
        {
            get { return subtitleText; }
            set
            {
                // Older layouts described torrent streaming as "without downloading".
                // Streaming still receives pieces into VideoShelf's temporary cache, so
                // normalise that legacy copy wherever this visual control is used.
                subtitleText = string.Equals(value, "Play without downloading", StringComparison.OrdinalIgnoreCase)
                    ? "Streams via temporary cache"
                    : (value ?? "");
                Invalidate();
            }
        }

        public MockupActionButton()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = pressed ? Color.FromArgb(18, 31, 42) : hot ? Color.FromArgb(14, 28, 39) : Color.FromArgb(8, 18, 26);
            if (!Enabled) bg = Color.FromArgb(9, 16, 22);
            using (var b = new SolidBrush(bg)) UiPaint.FillRound(g, b, r, 7);
            using (var p = new Pen(Enabled ? Accent : Color.FromArgb(48, 60, 70), 2f)) UiPaint.DrawRound(g, p, new Rectangle(1, 1, Width - 3, Height - 3), 7);

            Color iconColor = Enabled ? Accent : Color.FromArgb(89, 101, 112);
            using (var f = UiPaint.IconFont(21f))
                TextRenderer.DrawText(g, Glyph, f, new Rectangle(18, 12, 38, 38), iconColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (var f = new Font("Segoe UI", 11.6f, FontStyle.Bold))
                TextRenderer.DrawText(g, TitleText, f, new Rectangle(68, 10, Width - 80, 25), Enabled ? Color.White : Color.FromArgb(120, 130, 140), TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                TextRenderer.DrawText(g, SubtitleText, f, new Rectangle(68, 37, Width - 80, 18), Enabled ? Color.FromArgb(205, 217, 229) : Color.FromArgb(95, 106, 116), TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 10, Height - 10));
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && Enabled) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
