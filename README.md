# VideoShelf

VideoShelf is a lightweight Windows desktop video library browser built with C# WinForms. It treats each immediate subfolder of a chosen library directory as a person/collection, shows a portrait card for that folder, finds local videos recursively, and can optionally search a user-configured torrent-metadata source.

## Current baseline — v1.6

- Xdolf-inspired dark visual theme and owner-drawn tooltips.
- Folder-per-person/collection library with recursive local video discovery.
- **Add folder** directly inside the app, with Ctrl+N shortcut.
- Exact display names are preserved even when Windows forbids a character in the physical folder name. For example, entering `re:zero` creates a safe disk folder but VideoShelf still displays and searches for `re:zero`.
- Portrait lookup through DuckDuckGo Images, local cache, manual override, and browser fallback.
- Local video playback through the Windows default player.
- Optional online torrent-metadata search with title, resolution, size, seeders, leechers, source, publication date and artwork.
- Online discovery is metadata-only. VideoShelf does **not** transfer the referenced video while searching or displaying results.
- **↓ Download locally** starts a persistent torrent transfer only after the user explicitly chooses it and selects a destination folder.
- **▶ Stream locally** starts a temporary torrent session inside VideoShelf and plays the selected video through an embedded LibVLC player.
- Multi-file torrents expose their supported video files in the streaming window; the selected file is prioritised and unrelated files are set not to download.
- Streaming uses a temporary cache under `%LOCALAPPDATA%\VideoShelf\StreamingCache` and removes it on player close on a best-effort basis.
- Online results with `0` seeders are rejected and never displayed.
- 2160p/4K, 1080p, 720p, and Other resolution filtering.
- Search-engine-only result thumbnails through DuckDuckGo Images.
- Saved Torznab API keys protected with Windows DPAPI.
- Existing `%LOCALAPPDATA%\PeopleShelf` settings/caches are migrated automatically to `%LOCALAPPDATA%\VideoShelf` on first use.

## Run

The packaged Windows artifact contains `VideoShelf.exe` plus a `TransferHost` folder. Keep those together. `VideoShelf.exe` remains the lightweight .NET Framework WinForms library application, while the bundled self-contained transfer runtime supplies MonoTorrent and LibVLC for downloads and streaming.

When running directly from source, double-click `Start.cmd`. It compiles the main app using the .NET Framework C# compiler. If a .NET SDK is installed and the transfer runtime has not already been built, `Start.cmd` also publishes it to `TransferHostRuntime`.

For development, `VideoShelf.csproj` targets .NET Framework 4.8. `TransferHost/VideoShelf.TransferHost.csproj` targets .NET 8 for the integrated torrent/download/player runtime.

## Adding collections

Choose a library folder, then use **+ Add folder** or press **Ctrl+N**. The collection is created immediately under the selected library and the people/collection view refreshes automatically.

Windows-invalid filename characters are translated only for the underlying directory. The original name is stored in `.videoshelf-name`, so a title such as `re:zero` remains exactly `re:zero` throughout the VideoShelf interface and online/image searches.

## Online discovery

VideoShelf does not require a hard-coded indexer in its application logic. Configure a Torznab-compatible endpoint from software such as Jackett or Prowlarr, or another supported XML/RSS metadata search feed, from **Online source** inside the app.

Searching retrieves only the feed/indexer response needed to display the result list, plus small search-engine artwork thumbnails. It does not fetch the referenced video payload. Results are filtered again at render time so an item reporting zero seeders cannot appear.

## Download locally

Select a seeded result and choose **↓ Download locally**. VideoShelf asks for a destination before starting the transfer. The bundled MonoTorrent runtime then resolves the magnet/torrent metadata, connects to peers, downloads the torrent persistently and shows progress, current torrent state, transfer rate and received data.

Cancelling does not intentionally delete partial persistent data. Completed downloads remain in the selected destination.

## Stream locally

Select a seeded result and choose **▶ Stream locally**, or double-click/press Enter on it. VideoShelf opens its own streaming window rather than handing the result to an external torrent application.

The bundled MonoTorrent engine retrieves torrent metadata, identifies supported video files and exposes the selected file through a localhost HTTP streaming endpoint. The embedded LibVLCSharp player consumes that local stream. For multi-file torrents, VideoShelf lets the user choose the video and prioritises only that selection while marking unrelated torrent files as not to download.

Streaming still requires torrent pieces to be received locally as playback progresses. Those pieces go into a temporary VideoShelf streaming cache rather than a permanent library download. Closing the player stops the torrent session and removes that temporary cache on a best-effort basis.

## Search-engine images

Portraits and online-result thumbnails use DuckDuckGo Images only. This is a public search endpoint rather than an official image API, so automated lookup can occasionally be rate-limited or require a browser check. VideoShelf does not attempt to bypass those checks; cached images remain available and unresolved items keep their placeholder.

## Source layout

- `VideoShelf.cs` — entry point, shared models, and portrait card control
- `Shelf.Core.cs` — main window construction and shared UI helpers
- `Shelf.Library.cs` — local library scanning, portraits, navigation, and folder creation
- `Shelf.Online.cs` — metadata results and Stream/Download actions
- `TransferBridge.cs` — launches the bundled VideoShelf transfer runtime from the legacy WinForms shell
- `TransferHost/` — .NET 8 MonoTorrent + LibVLC download and embedded streaming runtime
- `FolderManagement.cs` — safe folder creation, display-name aliases, and the `re:zero` self-test
- `PortraitLookup.cs` — portrait search/cache
- `OnlineSearch.cs` — metadata-source search/settings
- `OnlineThumbnailLookup.cs` — search-engine result thumbnails
- `XdolfTheme.cs` — Xdolf-inspired palette, controls and tooltip rendering
- `ScreenshotHarness.cs` — deterministic UI proof/capture helpers
- `AppDataPaths.cs` — VideoShelf data paths and one-time PeopleShelf migration
- `Start.cmd` — main compiler/launcher and optional local TransferHost publish
- `VideoShelf.csproj` — .NET Framework 4.8 main app project

## Build validation

The Windows GitHub Actions workflow compiles the main app, runs the `re:zero` folder test, publishes the self-contained transfer runtime, performs an offline runtime test that loads MonoTorrent and the bundled LibVLC native libraries, renders the real library UI, and optionally exercises live metadata-only discovery when public feeds are reachable. It does not use a media-download fallback for search/display validation.

The `VideoShelf-windows` artifact is the complete runnable package and contains both the main executable and the bundled transfer runtime.
