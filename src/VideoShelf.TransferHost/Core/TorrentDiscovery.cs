using MonoTorrent;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal static class TorrentDiscovery
{
    static readonly TimeSpan WarmupDuration = TimeSpan.FromSeconds(7);
    static readonly TimeSpan PrefetchTimeout = TimeSpan.FromSeconds(20);
    static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    const string WarmupMagnet = "magnet:?xt=urn:btih:8D7A39C1F0E4B6297A31D0645B2C8E93F1A7D450&dn=VideoShelfDiscoveryWarmup";

    public static string SharedStateRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoShelf",
        "TorrentDiscovery");

    public static async Task WarmAsync(CancellationToken token)
    {
        await RunIsolatedAsync(async (session, scratch) =>
        {
            var manager = await session.AddAsync(WarmupMagnet, scratch, token);
            await TorrentSession.StartMetadataOnlyAsync(manager);
            await Task.Delay(WarmupDuration, token);
        }, token);
    }

    public static async Task PrefetchAsync(string source, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(source)) return;

        await RunIsolatedAsync(async (session, scratch) =>
        {
            var manager = await session.AddAsync(source, scratch, token);
            if (manager.HasMetadata) return;

            await TorrentSession.StartMetadataOnlyAsync(manager);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(PrefetchTimeout);
            try
            {
                await manager.WaitForMetadataAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // A prefetch is opportunistic. The foreground stream path has its own retry flow.
            }
        }, token);
    }

    static async Task RunIsolatedAsync(Func<TorrentSession, string, Task> action, CancellationToken token)
    {
        string shared = SharedStateRoot;
        string working = Path.Combine(Path.GetTempPath(), "VideoShelf-discovery-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(working, "scratch");
        Directory.CreateDirectory(shared);
        Directory.CreateDirectory(working);
        Directory.CreateDirectory(scratch);
        CopyTreeBestEffort(shared, working);

        TorrentSession? session = null;
        try
        {
            session = new TorrentSession(working);
            await action(session, scratch);
        }
        finally
        {
            // Magnet metadata is useful to the foreground stream the instant it is
            // received. Publish .torrent cache entries before shutting the helper
            // down so a slow tracker/DHT shutdown can never hide successful work.
            CopyTorrentMetadataBestEffort(working, shared);

            if (session != null)
            {
                TorrentManager? manager = session.Manager;
                if (manager != null)
                {
                    try
                    {
                        using var stopDeadline = new CancellationTokenSource(StopTimeout + TimeSpan.FromSeconds(1));
                        await manager.StopAsync(StopTimeout).WaitAsync(stopDeadline.Token);
                    }
                    catch
                    {
                        // Discovery is opportunistic. Never let shutdown hold the
                        // foreground app hostage for minutes.
                    }
                }
                try { session.Engine.Dispose(); } catch { }
            }

            // DHT state is generally written as the engine stops. Merge whatever
            // completed within the bounded shutdown window, but never block on it.
            CopyTreeBestEffort(working, shared, skipDirectoryName: "scratch");
            try { Directory.Delete(working, true); } catch { }
        }
    }

    static void CopyTorrentMetadataBestEffort(string sourceRoot, string destinationRoot)
    {
        if (!Directory.Exists(sourceRoot)) return;
        string[] files;
        try { files = Directory.GetFiles(sourceRoot, "*.torrent", SearchOption.AllDirectories); }
        catch { return; }

        foreach (string source in files)
        {
            try
            {
                string relative = Path.GetRelativePath(sourceRoot, source);
                string first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (first.Equals("scratch", StringComparison.OrdinalIgnoreCase)) continue;
                string destination = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
            catch
            {
                // The foreground stream may already be opening the same cache file.
            }
        }
    }

    static void CopyTreeBestEffort(string sourceRoot, string destinationRoot, string? skipDirectoryName = null)
    {
        if (!Directory.Exists(sourceRoot)) return;
        foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            try
            {
                string relative = Path.GetRelativePath(sourceRoot, source);
                if (!string.IsNullOrEmpty(skipDirectoryName))
                {
                    string first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                    if (first.Equals(skipDirectoryName, StringComparison.OrdinalIgnoreCase)) continue;
                }
                string destination = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
            catch
            {
                // Another foreground stream may have the cache open. Prefetch must never disrupt playback.
            }
        }
    }

    public static void SelfTest()
    {
        if (!MagnetLink.TryParse(WarmupMagnet, out _))
            throw new InvalidOperationException("Torrent discovery warmup magnet is invalid.");
        string expectedParent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoShelf");
        if (!Path.GetFullPath(SharedStateRoot).StartsWith(Path.GetFullPath(expectedParent), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Torrent discovery cache is not stored under VideoShelf LocalAppData.");
        if (StopTimeout > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("Torrent discovery shutdown must remain bounded.");
    }
}
