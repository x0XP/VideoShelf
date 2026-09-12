namespace VideoShelf.TransferHost;

/// <summary>
/// Applies the release-aware default torrent video selection through the real
/// selector control without reflecting into OptimizedStreamForm internals.
/// </summary>
internal sealed class TorrentVideoSelectionUiController : IDisposable
{
    readonly OptimizedStreamForm form;
    readonly string releaseTitle;
    readonly DarkComboBox fileChoice;
    bool scheduled;
    bool applied;
    bool disposed;

    TorrentVideoSelectionUiController(OptimizedStreamForm form, string releaseTitle)
    {
        this.form = form ?? throw new ArgumentNullException(nameof(form));
        this.releaseTitle = releaseTitle ?? string.Empty;
        fileChoice = FindControl<DarkComboBox>(form)
            ?? throw new InvalidOperationException("Streaming video selector was not found.");

        fileChoice.EnabledChanged += OnSelectorChanged;
        fileChoice.SelectedIndexChanged += OnSelectorChanged;
        form.FormClosed += OnClosed;
    }

    public static TorrentVideoSelectionUiController Attach(OptimizedStreamForm form, string releaseTitle)
        => new(form, releaseTitle);

    void OnSelectorChanged(object? sender, EventArgs e)
    {
        if (disposed || applied || scheduled || !fileChoice.Enabled || fileChoice.Items.Count == 0)
            return;

        // Enabled is raised while OptimizedStreamForm is still finishing its metadata/player
        // setup. Queue the correction so its own initial SelectedIndex assignment completes first.
        if (!form.IsHandleCreated || form.IsDisposed)
            return;

        scheduled = true;
        try
        {
            form.BeginInvoke((Action)ApplySelection);
        }
        catch (InvalidOperationException)
        {
            scheduled = false;
        }
    }

    void ApplySelection()
    {
        scheduled = false;
        if (disposed || applied || form.IsDisposed || !fileChoice.Enabled || fileChoice.Items.Count == 0)
            return;

        string[] paths = fileChoice.Items.Cast<object>()
            .Select(item => ExtractPath(item?.ToString()))
            .ToArray();

        int selectorIndex = ChooseSelectorIndex(releaseTitle, paths);
        if (selectorIndex >= 0 && fileChoice.SelectedIndex != selectorIndex)
            fileChoice.SelectedIndex = selectorIndex;

        applied = true;
    }

    internal static int ChooseSelectorIndex(string releaseTitle, IReadOnlyList<string> selectorPaths)
    {
        if (selectorPaths == null || selectorPaths.Count == 0) return -1;

        string[] naturalPaths = selectorPaths
            .OrderBy(path => path, TorrentVideoSelection.NaturalPathComparer)
            .ToArray();

        int naturalIndex = TorrentVideoSelection.ChooseDefaultIndex(releaseTitle ?? string.Empty, naturalPaths);
        if (naturalIndex < 0 || naturalIndex >= naturalPaths.Length) return -1;

        string chosenPath = naturalPaths[naturalIndex];
        for (int i = 0; i < selectorPaths.Count; i++)
            if (string.Equals(selectorPaths[i], chosenPath, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }

    internal static string ExtractPath(string? selectorText)
    {
        string value = selectorText ?? string.Empty;
        int suffix = value.LastIndexOf("  (", StringComparison.Ordinal);
        return suffix > 0 ? value[..suffix] : value;
    }

    public static void SelfTest()
    {
        string[] selectorOrder =
        {
            "Season 01/Show.S01E10.mkv",
            "Season 01/Show.S01E02.mkv",
            "Season 01/Show.S01E01.mkv"
        };
        int selected = ChooseSelectorIndex("Show Season 1 Complete 1080p", selectorOrder);
        if (selected != 2)
            throw new InvalidOperationException("Event-driven torrent video selector did not map the natural default back to the real selector index.");

        string rendered = "Season 01/Show.S01E02.mkv  (1.4 GB)";
        if (!ExtractPath(rendered).Equals("Season 01/Show.S01E02.mkv", StringComparison.Ordinal))
            throw new InvalidOperationException("Torrent video selector path extraction regressed.");
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

    void OnClosed(object? sender, FormClosedEventArgs e) => Dispose();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        fileChoice.EnabledChanged -= OnSelectorChanged;
        fileChoice.SelectedIndexChanged -= OnSelectorChanged;
        form.FormClosed -= OnClosed;
    }
}
