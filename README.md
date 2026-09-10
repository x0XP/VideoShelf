# VideoShelf

VideoShelf is a lightweight Windows desktop video library browser built with C# WinForms. It treats each immediate subfolder of a chosen library directory as a person/collection, shows a portrait card for that folder, finds local videos recursively, and can optionally search a user-configured Torznab-compatible source.

## Current baseline — v1.4

- Folder-per-person library with recursive local video discovery.
- **Add folder** directly inside the app, with Ctrl+N shortcut.
- Exact display names are preserved even when Windows forbids a character in the physical folder name. For example, entering `re:zero` creates a safe disk folder but VideoShelf still displays and searches for `re:zero`.
- Portrait lookup through DuckDuckGo Images, local cache, manual override, and browser fallback.
- Local video playback through the Windows default player.
- Optional Torznab-compatible online search with title, resolution, size, seeders, leechers, source, and publication date.
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

## Online source

VideoShelf does not hard-code torrent/indexer sites. Configure a Torznab-compatible endpoint from software such as Jackett or Prowlarr from **Online source** inside the app. Provider configuration remains outside VideoShelf.

Returned links are opened through the operating system. VideoShelf itself does not download, move, rename, delete, or upload local media.

## Search-engine images

Portraits and online-result thumbnails use DuckDuckGo Images only. This is a public search endpoint rather than an official image API, so automated lookup can occasionally be rate-limited or require a browser check. VideoShelf does not attempt to bypass those checks; cached images remain available and unresolved items keep their placeholder.

## Source layout

- `VideoShelf.cs` — entry point, shared models, and portrait card control
- `Shelf.Core.cs` — main window construction and shared UI helpers
- `Shelf.Library.cs` — local library scanning, portraits, navigation, and folder creation
- `Shelf.Online.cs` — online result/search/thumbnails UI
- `FolderManagement.cs` — safe folder creation, display-name aliases, and the `re:zero` self-test
- `PortraitLookup.cs` — portrait search/cache
- `OnlineSearch.cs` — Torznab search/settings
- `OnlineThumbnailLookup.cs` — search-engine result thumbnails
- `AppDataPaths.cs` — VideoShelf data paths and one-time PeopleShelf migration
- `Start.cmd` — minimal Windows compiler/launcher
- `VideoShelf.csproj` — Visual Studio/MSBuild project

## Build validation

The repository includes a Windows GitHub Actions workflow. Pushes and pull requests to `main` compile the same sources, run the folder-creation self-test using `re:zero`, and upload `VideoShelf.exe` as a workflow artifact.
