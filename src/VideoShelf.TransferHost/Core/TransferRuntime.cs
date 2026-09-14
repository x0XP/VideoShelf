namespace VideoShelf.TransferHost;

internal static class TransferRuntime
{
    public static void SelfTest()
    {
        TorrentSession.SelfTest();
        TorrentDiscovery.SelfTest();
        StreamingTorrentSession.SelfTest();
        TransferSourceResolver.SelfTest();
        TorrentVideoSelection.SelfTest();
        OptimizedStreamForm.EpisodeNavigationSelfTest();
        TransferUiCapture.Capture(Environment.CurrentDirectory);
    }
}
