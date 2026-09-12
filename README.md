# VideoShelf

VideoShelf is a lightweight Windows desktop video library browser built with C# WinForms. It treats each immediate subfolder of a chosen library directory as a collection, shows an artwork card for that folder, finds local videos recursively, and can optionally search a user-configured torrent-metadata source.

## Current baseline — v1.7

- Full Xdolf-inspired VideoShelf shell with custom title/status bars and permanent Home, Search, Collections, Downloads, Streaming and Settings navigation.
- Xdolf-style dark palette, owner-drawn tooltips, collection cards, filters, metadata surfaces, action controls, headers and scrollbars.
- Folder-per-collection library with recursive local video discovery.
- **+ Add collection** directly inside the app, with Ctrl+N shortcut.
- Exact display names are preserved even when Windows forbids a character in the physical folder name. For example, entering `re:zero` creates a safe disk folder but VideoShelf still displays and searches for `re:zero`.
- Collection artwork lookup through DuckDuckGo Images, local cache, manual override, and browser fallback.
- Local video playback through the Windows default player.
- Optional online torrent-metadata search rendered as stacked result cards with title, artwork, tags, resolution, size, seeders, leechers, source and publication date.
- Search results can be filtered by source, category, resolution and language. The language filter defaults to **English**.
- The English filter keeps releases explicitly marked English, multi-language releases and releases with no language marker, while excluding releases explicitly marked as another supported language. **All languages**, individual languages, **Multi-language** and **Unspecified** are available from the same filter.
- Selected online results appear in a dedicated inspector with aligned metadata and Download, Stream, Copy link and View files actions.
- Online discovery is metadata-only. VideoShelf does **not** transfer the referenced video while searching or displaying results.
- **Download locally** starts a persistent torrent transfer only after the user explicitly chooses it and selects a destination folder.
- **Stream locally** starts a temporary torrent session inside VideoShelf and plays the selected video through an embedded LibVLC player.
- **View files** resolves torrent metadata and displays the torrent manifest without intentionally starting normal media payload transfer.
- Multi-file torrents expose their supported video files in the streaming window; the selected file is prioritised and unrelated files are set not to download.
- Streaming uses a temporary cache under `%LOCALAPPDATA%\VideoShelf\StreamingCache` and removes it on player close on a best-effort basis.
- Online results with `0` seeders are rejected during parsing and filtered again before rendering, so they are never displayed.
- 2160p/4K, 1080p, 720p, and Other resolution filtering.
- Search-engine-only result thumbnails through DuckDuckGo Images.
- Saved Torznab API keys protected with Windows DPAPI.

## Run

The packaged Windows artifact contains `VideoShelf.exe` plus a `TransferHost` folder. Keep those together. `VideoShelf.exe` remains the lightweight .NET Framework WinForms library application, while the bundled self-contained transfer runtime supplies MonoTorrent and LibVLC for downloads, metadata inspection and streaming.

When running directly from source, double-click `Start.cmd`. It compiles the main app using the .NET Framework C# compiler. If a .NET SDK is installed and the transfer runtime has not already been built, `Start.cmd` also publishes it to `TransferHostRuntime`.

For development, `VideoShelf.csproj` targets .NET Framework 4.8. `TransferHost/VideoShelf.TransferHost.csproj` targets .NET 8 for the integrated torrent/download/player runtime.

## Adding collections

Choose a library folder, then use **+ Add collection** or press **Ctrl+N**. The collection is created immediately under the selected library and the collection view refreshes automatically.

Windows-invalid filename characters are translated only for the underlying directory. The original name is stored in `.videoshelf-name`, so a title such as `re:zero` remains exactly `re:zero` throughout the VideoShelf interface and online/image searches.

## Online discovery

VideoShelf does not require a hard-coded indexer in its application logic. Configure a Torznab-compatible endpoint from software such as Jackett or Prowlarr, or another supported XML/RSS metadata search feed, from the source configuration inside the app.

Searching retrieves only the feed/indexer response needed to display the result list, plus small search-engine artwork thumbnails. It does not fetch the referenced video payload. Results are filtered again at render time so an item reporting zero seeders cannot appear.

Language filtering is derived from language markers in release titles. VideoShelf recognises English, Japanese, Spanish, French, German, Italian, Portuguese, Korean, Chinese, Russian, Hindi, Arabic, Thai, Dutch, Polish, Turkish and Indonesian markers, along with multi-language and unspecified releases. The default English view deliberately retains unlabelled releases so ordinary releases that omit an explicit language tag are not hidden.

## View files

Select a seeded online result and choose **View files** to inspect the torrent contents before starting a transfer. The bundled MonoTorrent runtime resolves the torrent metadata and opens a manifest window. VideoShelf's runtime self-test verifies that the installed MonoTorrent version exposes the metadata-only startup path required for this operation.

## Download locally

Select a seeded result and choose **Download locally**. VideoShelf asks for a destination before starting the transfer. The bundled MonoTorrent runtime then resolves the magnet/torrent metadata, connects to peers, downloads the torrent persistently and shows progress, current torrent state, transfer rate and received data.

Cancelling does not intentionally delete partial persistent data. Completed downloads remain in the selected destination.

## Stream locally

Select a seeded result and choose **Stream locally**, or double-click/press Enter on it. VideoShelf opens its own streaming window rather than handing the result to an external torrent application.

The bundled MonoTorrent engine retrieves torrent metadata, identifies supported video files and exposes the selected file through a localhost HTTP streaming endpoint. The embedded LibVLCSharp player consumes that local stream. For multi-file torrents, VideoShelf lets the user choose the video and prioritises only that selection while marking unrelated torrent files as not to download.

Streaming still requires torrent pieces to be received locally as playback progresses. Those pieces go into a temporary VideoShelf streaming cache rather than a permanent library download. Closing the player stops the torrent session and removes that temporary cache on a best-effort basis.

## Search-engine images

Collection artwork and online-result thumbnails use DuckDuckGo Images only. Collection artwork searches begin with the collection name itself and then use neutral artwork/image fallbacks if needed. This is a public search endpoint rather than an official image API, so automated lookup can occasionally be rate-limited or require a browser check. VideoShelf does not attempt to bypass those checks; cached images remain available and unresolved items keep their placeholder.

## Source layout

The repository is grouped by responsibility rather than keeping the main application source flat:

- `src/VideoShelf/Application/` — main application entry point and shared application models.
- `src/VideoShelf/Infrastructure/` — application-data paths, safe folder creation and display-name aliases.
- `src/VideoShelf/Branding/` — runtime brand/icon loading.
- `src/VideoShelf/Search/` — metadata search, search relevance, portrait lookup and online thumbnail lookup.
- `src/VideoShelf/Media/` — bridge between the lightweight shell and the bundled transfer runtime.
- `src/VideoShelf/UI/Controls/` — reusable Xdolf-inspired controls and filter/inspector helpers.
- `src/VideoShelf/UI/Shelf/` — the partial `Shelf` window implementation, split by layout, library, online, branding and visual responsibilities.
- `src/VideoShelf/UI/Theme/` — shared Xdolf-inspired palette and tooltip rendering.
- `src/VideoShelf/Diagnostics/` — deterministic screenshot/test harness used by CI.
- `TransferHost/Core/` — MonoTorrent source resolution, transfer runtime helpers and torrent-video selection.
- `TransferHost/Player/` — embedded LibVLC player and fullscreen/player controls.
- `TransferHost/UI/` — transfer/file-list UI and themed transfer controls.
- `TransferHost/Diagnostics/` — transfer UI capture used by the runtime self-test.
- `TransferHost/EntryPoint.cs`, `TransferHost/Program.cs` and `TransferHost/VideoShelf.TransferHost.csproj` — transfer-host startup and project definition.
- `BuildBrandAssets.ps1` — brand asset generation kept at repository root because both the classic project and launcher invoke it directly.
- `Start.cmd` — main compiler/launcher and optional local TransferHost publish.
- `VideoShelf.csproj` — .NET Framework 4.8 main app project.
- `installer/` — Inno Setup installer definition.

## Build validation

The Windows GitHub Actions workflow compiles the main app, runs the `re:zero` folder alias test, publishes the self-contained transfer runtime, validates MonoTorrent/LibVLC and metadata-only file inspection support, renders the actual compiled Home/collection/search controls, attempts a real `re:zero` artwork pull, and attempts live seeded torrent-metadata discovery without using a media-download fallback.

The required visual captures are deterministic; external image/feed availability is kept separate so a third-party service cannot invalidate the core UI build. The live proof steps still run when the public sources are reachable.

The `VideoShelf-windows` artifact is the complete runnable package and contains both the main executable and bundled transfer runtime.
