using System.Reflection;
using MonoTorrent.Client;

namespace VideoShelf.TransferHost;

internal static class TransferRuntime
{
    public static void SelfTest()
    {
        TorrentSession.SelfTest();
        MethodInfo? metadataStart = typeof(TorrentManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "StartAsync" &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType == typeof(bool));
        if (metadataStart == null)
            throw new InvalidOperationException("MonoTorrent metadata-only startup is unavailable; View files cannot be guaranteed payload-free.");
    }
}
