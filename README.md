# VideoShelf

VideoShelf is a lightweight Windows desktop video library browser built with C# WinForms. It treats each immediate subfolder of a chosen library directory as a person/collection, shows a portrait card for that folder, finds local videos recursively, and can optionally search a user-configured Torznab-compatible source.

## Current baseline — v1.5

- Xdolf-inspired dark visual theme and owner-drawn tooltips.
- Folder-per-person/collection library with recursive local video discovery.
- **Add folder** directly inside the app, with Ctrl+N shortcut.
- Exact display names are preserved even when Windows forbids a character in the physical folder name. For example, entering `re:zero` creates a safe disk folder but VideoShelf still displays and searches for `re:zero`.
- Portrait lookup through DuckDuckGo Images, local cache, manual override, and browser fallback.
- Local video playback through the Windows default player.
- Optional online torrent-metadata search with title, resolution, size, seeders, leechers, source, publication date and artwork.
- Online discovery is metadata-only. VideoShelf does **not** download the video while searching or displaying results.
- **Stream locally** is an explicit user action that hands the selected torrent/magnet link to the registered local Windows handler. Any torrent data transfer happens in that local handler, not inside VideoShelf's discovery process.
- Online results with `0` seeders are rejected and never displayed.
- 2160p/4K, 1080p, 720p, and Other resolution filtering.
- Search-engine-only result thumbnails through DuckDuckGo Images.
- 96×54 progressive thumbnail loading with local caching and title de-duplication.
- Saved Torznab API keys protected with Windows DPAPI.
- Existing `%LOCALAPPDATA%\PeopleShelf` settings/caches are migrated automatically to `%LOCALAPPDATA%\VideoShelf` on first use.

## Run

On Windows, double-click `Start.cmd`. It compiles `VideoShelf.exe` using the .NET Framework C# compiler already included with Windows/.NET Framework 4.x and then launches it.

For development, `VideoShelf.csproj` targets .NET Framework 4.8 and can also be opened in Visual Studio/MSBuild.

## Adding collections

Choose a library folder, then use **+ Add folder** or press **Ctrl+N**. The collection is created immediately under the selected library and the people/collection view refreshes automatically.

Windows-invalid filename characters are translated only for the underlying directory. The original name is stored in `.videoshelf-name`, so a title such as `re:zero` remains exactly `re:zero` throughout the VideoShelf interface and online/image searches.

## Online discovery and local streaming

VideoShelf does not hard-code torrent/indexer providers into the application. Configure a Torznab-compatible endpoint from software such as Jackett or Prowlarr, or another supported XML/RSS metadata search feed, from **Online source** inside the app.

Searching retrieves only the feed/indexer response needed to display the result list, plus small search-engine artwork thumbnails. It does not fetch the referenced video payload. Each seeded result is displayed with the information needed to choose between releases.

When the user deliberately chooses **Stream locally** (or double-clicks/presses Enter on a result), VideoShelf passes that result's torrent/magnet link to the registered Windows handler. Whether the local handler streams sequentially or performs a conventional torrent download depends on that external application and its configuration.

VideoShelf itself does not download, move, rename, delete, or upload local media during online discovery.

## Search-engine images

Portraits and online-result thumbnails use DuckDuckGo Images only. This is a public search endpoint rather than an official image API, so automated lookup can occasionally be rate-limited or require a browser check. VideoShelf does not attempt to bypass those checks; cached images remain available and unresolved items keep their placeholder.

## Source layout

- `VideoShelf.cs` — entry point, shared models, and portrait card control
- `Shelf.Core.cs` — main window construction and shared UI helpers
- `Shelf.Library.cs` — local library scanning, portraits, navigation, and folder creation
- `Shelf.Online.cs` — online result/search/thumbnails UI and local stream handoff
- `FolderManagement.cs` — safe folder creation, display-name aliases, and the `re:zero` self-test
- `PortraitLookup.cs` — portrait search/cache
- `OnlineSearch.cs` — metadata-source search/settings
- `OnlineThumbnailLookup.cs` — search-engine result thumbnails
- `XdolfTheme.cs` — Xdolf-inspired palette, controls and tooltip rendering
- `ScreenshotHarness.cs` — deterministic UI proof/capture helpers
- `AppDataPaths.cs` — VideoShelf data paths and one-time PeopleShelf migration
- `Start.cmd` — minimal Windows compiler/launcher
- `VideoShelf.csproj` — Visual Studio/MSBuild project

## Build validation

The repository includes a Windows GitHub Actions workflow. Pushes and pull requests to `main` compile the same sources, run the folder-creation self-test using `re:zero`, render the real library UI, and exercise metadata-only online discovery without intentionally downloading any video payload. The executable and successful screenshots are uploaded as workflow artifacts.
