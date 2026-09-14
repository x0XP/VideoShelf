using LibVLCSharp.Shared;

namespace VideoShelf.TransferHost;

internal sealed partial class OptimizedStreamForm
{
    MediaPlayer? episodeNavigationPlayer;
    bool episodeAutoAdvanceInProgress;

    protected override void OnShown(EventArgs e)
    {
        // Attach before the Shown event starts asynchronous torrent/player initialisation.
        // The shared UI timer will bind EndReached as soon as the MediaPlayer exists.
        uiTimer.Tick += OnEpisodeNavigationTick;
        base.OnShown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try { uiTimer.Tick -= OnEpisodeNavigationTick; } catch { }
        SetEpisodeNavigationPlayer(null);
        base.OnFormClosed(e);
    }

    void OnEpisodeNavigationTick(object? sender, EventArgs e)
    {
        SetEpisodeNavigationPlayer(player);
    }

    void SetEpisodeNavigationPlayer(MediaPlayer? next)
    {
        if (ReferenceEquals(episodeNavigationPlayer, next)) return;
        if (episodeNavigationPlayer != null)
        {
            try { episodeNavigationPlayer.EndReached -= OnEpisodeEndReached; } catch { }
        }

        episodeNavigationPlayer = next;
        if (episodeNavigationPlayer != null)
        {
            try { episodeNavigationPlayer.EndReached += OnEpisodeEndReached; } catch { }
        }
    }

    void OnEpisodeEndReached(object? sender, EventArgs e)
    {
        if (sender is not MediaPlayer endedPlayer) return;
        int endedRevision = Volatile.Read(ref playbackRevision);
        if (closing || IsDisposed || Disposing || !IsHandleCreated) return;

        try
        {
            BeginInvoke(new Action(async () => await AutoAdvanceEpisodeAsync(endedPlayer, endedRevision)));
        }
        catch (InvalidOperationException) { }
    }

    async Task AutoAdvanceEpisodeAsync(MediaPlayer endedPlayer, int endedRevision)
    {
        if (closing || IsDisposed || Disposing || episodeAutoAdvanceInProgress) return;
        if (!ReferenceEquals(player, endedPlayer) || endedRevision != playbackRevision) return;
        if (selected == null || playable.Count < 2) return;

        int currentIndex = playable.IndexOf(selected);
        if (currentIndex < 0) return;

        int nextIndex = FindNextEpisodeIndex(playable.Select(file => file.Path).ToArray(), currentIndex);
        if (nextIndex < 0 || nextIndex >= playable.Count) return;

        ITorrentManagerFile nextFile = playable[nextIndex];
        episodeAutoAdvanceInProgress = true;
        try
        {
            status.Text = "Episode complete • loading next episode…";
            videoOverlay.Text = "Loading next episode…";
            videoOverlay.Visible = true;

            // Update the visible selector without triggering its SelectedIndexChanged playback
            // handler twice. That handler checks against selected before starting playback.
            selected = nextFile;
            fileChoice.SelectedIndex = nextIndex;
            await PlayFileAsync(nextFile);
        }
        finally
        {
            episodeAutoAdvanceInProgress = false;
        }
    }

    internal static int FindNextEpisodeIndex(IReadOnlyList<string> naturallyOrderedPaths, int currentIndex)
    {
        if (naturallyOrderedPaths == null || currentIndex < 0 || currentIndex >= naturallyOrderedPaths.Count - 1)
            return -1;

        string currentIdentity = EpisodeIdentity(Path.GetFileNameWithoutExtension(naturallyOrderedPaths[currentIndex] ?? string.Empty));
        if (currentIdentity.Length == 0) return -1;

        for (int i = currentIndex + 1; i < naturallyOrderedPaths.Count; i++)
        {
            string candidateIdentity = EpisodeIdentity(Path.GetFileNameWithoutExtension(naturallyOrderedPaths[i] ?? string.Empty));
            if (candidateIdentity.Length == 0) continue;
            if (candidateIdentity.Equals(currentIdentity, StringComparison.OrdinalIgnoreCase)) continue;
            return i;
        }

        return -1;
    }

    internal static void EpisodeNavigationSelfTest()
    {
        string[] seasonPack =
        {
            "Season 01/Example.Show.S01E01.1080p.mkv",
            "Extras/Trailer.mkv",
            "Season 01/Example.Show.S01E02.1080p.mkv",
            "Season 01/Example.Show.S01E02.720p.mkv",
            "Season 02/Example.Show.S02E01.1080p.mkv"
        };

        if (FindNextEpisodeIndex(seasonPack, 0) != 2)
            throw new InvalidOperationException("Next-episode selection did not skip non-episode files.");
        if (FindNextEpisodeIndex(seasonPack, 2) != 4)
            throw new InvalidOperationException("Next-episode selection did not skip a duplicate episode or cross the season boundary.");
        if (FindNextEpisodeIndex(seasonPack, 4) != -1)
            throw new InvalidOperationException("Next-episode selection should stop at the final episode.");

        string[] animePack =
        {
            "[Group] Example Show - 82 (1080p).mkv",
            "[Group] Example Show - 83 (1080p).mkv"
        };
        if (FindNextEpisodeIndex(animePack, 0) != 1)
            throw new InvalidOperationException("Absolute anime episode auto-advance regressed.");

        string[] movies = { "Movie.Part.One.mkv", "Movie.Part.Two.mkv" };
        if (FindNextEpisodeIndex(movies, 0) != -1)
            throw new InvalidOperationException("Auto-advance must not treat arbitrary multi-file torrents as episodic.");
    }
}
