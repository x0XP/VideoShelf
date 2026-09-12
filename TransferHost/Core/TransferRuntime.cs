namespace VideoShelf.TransferHost;

internal static class TransferRuntime
{
    public static void SelfTest()
    {
        TorrentSession.SelfTest();
        StreamingTorrentSession.SelfTest();
        TransferSourceResolver.SelfTest();
        TorrentVideoSelection.SelfTest();
        TransferUiCapture.Capture(Environment.CurrentDirectory);
    }
}
