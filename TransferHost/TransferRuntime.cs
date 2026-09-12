using System.Reflection;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal static class TransferRuntime
{
    public static void SelfTest()
    {
        TorrentSession.SelfTest();
        StreamingTorrentSession.SelfTest();
        TransferSourceResolver.SelfTest();
        TorrentVideoSelection.SelfTest();
        TorrentVideoSelectionUiController.SelfTest();
        MethodInfo? metadataStart = typeof(TorrentManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "StartAsync" &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType == typeof(bool));
        if (metadataStart == null)
            throw new InvalidOperationException("MonoTorrent metadata-only startup is unavailable; View files cannot be guaranteed payload-free.");

        // Render the real transfer controls in fixture mode. This catches broken player/download/file-list
        // layouts without starting a torrent or downloading any media payload.
        TransferUiCapture.Capture(Environment.CurrentDirectory);
    }
}
