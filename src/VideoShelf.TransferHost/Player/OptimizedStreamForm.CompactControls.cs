namespace VideoShelf.TransferHost;

internal sealed partial class OptimizedStreamForm
{
    readonly PlayerIconButton subtitleButton = new() { IconKind = PlayerIconKind.Subtitles, AccessibleName = "Subtitles" };
    readonly PlayerIconButton volumeButton = new() { IconKind = PlayerIconKind.Volume, AccessibleName = "Volume" };
    readonly ContextMenuStrip subtitleMenu = new()
    {
        ShowImageMargin = false,
        ShowCheckMargin = true,
        BackColor = Theme.Panel,
        ForeColor = Theme.Text,
        Font = new Font("Segoe UI", 9.2f),
        Renderer = new ToolStripProfessionalRenderer(new CompactPlayerColorTable())
    };
    readonly ToolStripDropDown volumePopup = new()
    {
        AutoClose = true,
        AutoSize = false,
        Padding = Padding.Empty,
        BackColor = Theme.Panel,
        Renderer = new ToolStripProfessionalRenderer(new CompactPlayerColorTable())
    };
    readonly ToolTip compactControlTips = new();
    Panel? volumePopupPanel;

    void InitializeCompactPlayerControls(Panel bottom)
    {
        // Keep the existing combo/volume logic as the single source of truth, but move
        // its UI behind compact player controls so the transport bar stays uncluttered.
        subtitleChoice.Visible = false;

        subtitleButton.SetBounds(384, 9, 42, 34);
        volumeButton.SetBounds(434, 9, 42, 34);
        bottom.Controls.Add(subtitleButton);
        bottom.Controls.Add(volumeButton);
        subtitleButton.BringToFront();
        volumeButton.BringToFront();

        compactControlTips.SetToolTip(subtitleButton, "Subtitles");
        compactControlTips.SetToolTip(volumeButton, "Volume");

        subtitleButton.Click += (_, _) => ShowSubtitleMenu();
        volumeButton.Click += (_, _) => ShowVolumePopup();

        volumePopupPanel = new Panel
        {
            BackColor = Theme.Panel,
            Size = new Size(246, 60),
            Margin = Padding.Empty
        };

        // Reparent the existing volume controls into the popup. Their existing event
        // handlers continue to drive LibVLC exactly as before.
        mute.Parent = volumePopupPanel;
        volume.Parent = volumePopupPanel;
        volumeLabel.Parent = volumePopupPanel;
        LayoutVolumePopupContents();
        mute.Visible = true;
        volume.Visible = true;
        volumeLabel.Visible = true;

        var host = new ToolStripControlHost(volumePopupPanel)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = volumePopupPanel.Size
        };
        volumePopup.Items.Add(host);
        volumePopup.Size = new Size(volumePopupPanel.Width + 2, volumePopupPanel.Height + 2);

        // These handlers run after the existing player handlers via BeginInvoke so the
        // icon/tooltip reflects the newly committed volume or mute state.
        volume.ValueCommitted += (_, _) => QueueCompactUiRefresh(UpdateCompactVolumeUi);
        mute.Click += (_, _) => QueueCompactUiRefresh(UpdateCompactVolumeUi);
        subtitleChoice.SelectedIndexChanged += (_, _) => QueueCompactUiRefresh(UpdateSubtitleTooltip);

        UpdateCompactVolumeUi();
        UpdateSubtitleTooltip();
    }

    void QueueCompactUiRefresh(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }

    void LayoutCompactPlayerControls()
    {
        subtitleButton.SetBounds(audioChoice.Right + 8, 9, 42, 34);
        volumeButton.SetBounds(subtitleButton.Right + 8, 9, 42, 34);
        LayoutVolumePopupContents();
    }

    void LayoutVolumePopupContents()
    {
        if (volumePopupPanel == null) return;
        mute.SetBounds(10, 13, 66, 34);
        volume.SetBounds(86, 15, 108, 30);
        volumeLabel.SetBounds(200, 19, 40, 22);
    }

    void ShowSubtitleMenu()
    {
        subtitleMenu.Items.Clear();

        if (subtitleOptions.Count <= 2 || !subtitleOptions.Any(x => x.Id >= 0))
        {
            subtitleMenu.Items.Add(new ToolStripMenuItem("No subtitles available") { Enabled = false });
        }
        else
        {
            for (int i = 0; i < subtitleOptions.Count; i++)
            {
                int index = i;
                SubtitleOption option = subtitleOptions[i];
                string label = CompactSubtitleLabel(option);
                var item = new ToolStripMenuItem(label)
                {
                    Checked = subtitleChoice.SelectedIndex == index,
                    CheckOnClick = false,
                    BackColor = Theme.Panel,
                    ForeColor = Theme.Text
                };
                item.Click += (_, _) =>
                {
                    subtitleChoice.SelectedIndex = index;
                    UpdateSubtitleTooltip();
                };
                subtitleMenu.Items.Add(item);
            }
        }

        Size preferred = subtitleMenu.GetPreferredSize(Size.Empty);
        subtitleMenu.Show(subtitleButton, new Point(0, -preferred.Height - 4));
    }

    static string CompactSubtitleLabel(SubtitleOption option)
    {
        string label = option.Name;
        if (label.StartsWith("Subtitles:", StringComparison.OrdinalIgnoreCase))
            label = label.Substring("Subtitles:".Length).Trim();
        return label.Length == 0 ? "Subtitles" : label;
    }

    void UpdateSubtitleTooltip()
    {
        string text = "Subtitles";
        int index = subtitleChoice.SelectedIndex;
        if (index >= 0 && index < subtitleOptions.Count)
            text += " • " + CompactSubtitleLabel(subtitleOptions[index]);
        compactControlTips.SetToolTip(subtitleButton, text);
    }

    void ShowVolumePopup()
    {
        UpdateCompactVolumeUi();
        LayoutVolumePopupContents();
        if (volumePopup.Visible)
        {
            volumePopup.Close(ToolStripDropDownCloseReason.CloseCalled);
            return;
        }

        volumePopup.Show(volumeButton, new Point(volumeButton.Width - volumePopup.Width, -volumePopup.Height - 4));
    }

    void UpdateCompactVolumeUi()
    {
        volumeButton.IconKind = currentVolume == 0 ? PlayerIconKind.VolumeMuted : PlayerIconKind.Volume;
        compactControlTips.SetToolTip(volumeButton, currentVolume == 0 ? "Volume • Muted" : $"Volume • {currentVolume}%");
    }
}

internal sealed class CompactPlayerColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Panel;
    public override Color MenuBorder => Theme.Outline;
    public override Color MenuItemBorder => Color.FromArgb(63, 94, 119);
    public override Color MenuItemSelected => Color.FromArgb(20, 48, 72);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(20, 48, 72);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(20, 48, 72);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(16, 39, 59);
    public override Color MenuItemPressedGradientMiddle => Color.FromArgb(16, 39, 59);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(16, 39, 59);
    public override Color ImageMarginGradientBegin => Theme.Panel;
    public override Color ImageMarginGradientMiddle => Theme.Panel;
    public override Color ImageMarginGradientEnd => Theme.Panel;
    public override Color SeparatorDark => Theme.Outline;
    public override Color SeparatorLight => Theme.Outline;
}