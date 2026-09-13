using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using MonoTorrent;

namespace VideoShelf.TransferHost;

internal static class TransferSourceResolver
{
    const int MaxPageBytes = 2 * 1024 * 1024;
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoShelf/1.7");
        return client;
    }

    public static Task<string> ResolveAsync(string source, string? pageUrl, CancellationToken token)
        => ResolveGeneralAsync(source, pageUrl, token);

    public static async Task<string> ResolveForStreamingAsync(string source, string? pageUrl, CancellationToken token)
    {
        source = (source ?? string.Empty).Trim();
        if (source.Length == 0) return source;

        // If the search result already supplied a valid magnet, keep it. The old
        // optimization replaced magnets with guessed /download/{id}.torrent URLs,
        // which can legitimately return 404 on mirrors even while the swarm works.
        if (MagnetLink.TryParse(source, out MagnetLink? magnet) && magnet != null)
            return source;

        // Some feeds expose only a direct .torrent URL but also carry a details page.
        // Prefer a magnet/info-hash recovered from that page so a stale direct URL
        // cannot abort streaming before peer discovery has a chance to run.
        string peerSource = await ResolvePeerSourceAsync(source, pageUrl, token).ConfigureAwait(false);
        if (peerSource.Length > 0)
            return peerSource;

        return await ResolveGeneralAsync(source, pageUrl, token).ConfigureAwait(false);
    }

    static async Task<string> ResolveGeneralAsync(string source, string? pageUrl, CancellationToken token)
    {
        source = (source ?? string.Empty).Trim();
        if (source.Length == 0) return source;

        if (LooksLikeTorrentMetadata(source)) return source;
        if (MagnetLink.TryParse(source, out MagnetLink? direct) && direct != null) return source;

        if (LooksLikePage(source))
        {
            string resolved = await ResolvePageAsync(source, token).ConfigureAwait(false);
            if (resolved.Length > 0) return resolved;
        }

        if (!string.IsNullOrWhiteSpace(pageUrl))
        {
            string resolved = await ResolvePageAsync(pageUrl.Trim(), token).ConfigureAwait(false);
            if (resolved.Length > 0) return resolved;
        }

        return NormalizeKnownPage(source);
    }

    static bool LooksLikeTorrentMetadata(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        string path = uri.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) || path.Contains("/download/");
    }

    static bool LooksLikePage(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        string path = uri.AbsolutePath.ToLowerInvariant();
        return !path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) &&
               (path.Contains("/view/") || path.Contains("/details/") || path.Contains("/torrent/") || path.Contains("/description"));
    }

    static async Task<string> ResolvePeerSourceAsync(string source, string? pageUrl, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(pageUrl))
        {
            string peer = await ResolvePeerPageAsync(pageUrl.Trim(), token).ConfigureAwait(false);
            if (peer.Length > 0) return peer;
        }

        if (LooksLikePage(source))
        {
            string peer = await ResolvePeerPageAsync(source, token).ConfigureAwait(false);
            if (peer.Length > 0) return peer;
        }

        return string.Empty;
    }

    static async Task<string> ResolvePageAsync(string pageUrl, CancellationToken token)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) ||
            (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps)) return string.Empty;

        try
        {
            string html = await ReadPageAsync(page, token).ConfigureAwait(false);
            if (html.Length == 0) return NormalizeKnownPage(pageUrl);
            string resolved = ExtractTransferSource(html, page);
            return resolved.Length > 0 ? resolved : NormalizeKnownPage(pageUrl);
        }
        catch (OperationCanceledException) { throw; }
        catch { return NormalizeKnownPage(pageUrl); }
    }

    static async Task<string> ResolvePeerPageAsync(string pageUrl, CancellationToken token)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) ||
            (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps)) return string.Empty;

        try
        {
            string html = await ReadPageAsync(page, token).ConfigureAwait(false);
            return html.Length == 0 ? string.Empty : ExtractPeerSource(html);
        }
        catch (OperationCanceledException) { throw; }
        catch { return string.Empty; }
    }

    static async Task<string> ReadPageAsync(Uri page, CancellationToken token)
    {
        using var response = await Http.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return string.Empty;
        if (response.Content.Headers.ContentLength is long length && length > MaxPageBytes) return string.Empty;

        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[32768];
        int total = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > MaxPageBytes) return string.Empty;
            memory.Write(buffer, 0, read);
        }

        return WebUtility.HtmlDecode(System.Text.Encoding.UTF8.GetString(memory.ToArray()));
    }

    internal static string ExtractTransferSource(string html, Uri page)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        Match torrent = Regex.Match(html, "href\\s*=\\s*[\\\"'](?<url>[^\\\"']+(?:\\.torrent(?:\\?[^\\\"']*)?))[\\\"']", RegexOptions.IgnoreCase);
        if (torrent.Success)
        {
            string href = WebUtility.HtmlDecode(torrent.Groups["url"].Value);
            if (Uri.TryCreate(page, href, out var absolute)) return absolute.AbsoluteUri;
        }

        string peer = ExtractPeerSource(html);
        return peer;
    }

    internal static string ExtractPeerSource(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        Match magnet = Regex.Match(html, "magnet:\\?[^\\s\\\"'<>]+", RegexOptions.IgnoreCase);
        if (magnet.Success)
        {
            string value = WebUtility.HtmlDecode(magnet.Value);
            if (MagnetLink.TryParse(value, out MagnetLink? parsed) && parsed != null) return value;
        }

        Match infoHash = Regex.Match(html, "info\\s*hash.{0,400}?([a-f0-9]{40})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (infoHash.Success) return "magnet:?xt=urn:btih:" + infoHash.Groups[1].Value;

        return string.Empty;
    }

    static string NormalizeKnownPage(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
        string path = uri.AbsolutePath.TrimEnd('/');
        int marker = path.IndexOf("/view/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return value;
        string id = path.Substring(marker + 6);
        if (id.Length == 0 || id.Any(c => !char.IsDigit(c))) return value;
        var builder = new UriBuilder(uri) { Path = "/download/" + id + ".torrent", Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }

    public static void SelfTest()
    {
        var page = new Uri("https://nyaa.example/view/12345");
        string hash = "0123456789abcdef0123456789abcdef01234567";
        string fromHash = ExtractTransferSource("<div>Info hash:</div><code>" + hash + "</code>", page);
        if (!fromHash.Equals("magnet:?xt=urn:btih:" + hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Torrent page info-hash resolution failed.");

        string magnet = "magnet:?xt=urn:btih:" + hash + "&dn=VideoShelf";
        string directTorrent = "https://nyaa.example/download/12345.torrent";
        string mixed = "<a href=\"" + magnet.Replace("&", "&amp;") + "\">Magnet</a><a href=\"/download/12345.torrent\">Torrent</a>";

        string preferredForGeneralTransfer = ExtractTransferSource(mixed, page);
        if (!preferredForGeneralTransfer.Equals(directTorrent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("General transfer resolution stopped preferring explicit torrent metadata.");

        string preferredForStreaming = ExtractPeerSource(WebUtility.HtmlDecode(mixed));
        if (!preferredForStreaming.Equals(magnet, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Streaming peer fallback did not prefer the magnet source.");

        string fromMagnet = ExtractTransferSource("<a href=\"" + magnet.Replace("&", "&amp;") + "\">Magnet</a>", page);
        if (!fromMagnet.Equals(magnet, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Torrent page magnet resolution failed.");

        string normal = ResolveAsync(magnet, page.AbsoluteUri, CancellationToken.None).GetAwaiter().GetResult();
        if (!normal.Equals(magnet, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Normal transfer resolution unexpectedly replaced a magnet source.");

        string streaming = ResolveForStreamingAsync(magnet, page.AbsoluteUri, CancellationToken.None).GetAwaiter().GetResult();
        if (!streaming.Equals(magnet, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Streaming unexpectedly replaced a valid magnet with a direct metadata URL.");
    }
}
