# VideoShelf

VideoShelf is a lightweight Windows desktop video library browser built with C# WinForms. It treats each immediate subfolder of a chosen library directory as a person/collection, shows a portrait card for that folder, finds local videos recursively, and can optionally search a user-configured Torznab-compatible source.

## Current baseline — v1.3

- Folder-per-person library with recursive local video discovery.
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

## Online source

VideoShelf does not hard-code torrent/indexer sites. Configure a Torznab-compatible endpoint from software such as Jackett or Prowlarr from **Online source** inside the app. Provider configuration remains outside VideoShelf.

Returned links are opened through the operating system. VideoShelf itself does not download, move, rename, delete, or upload local media.

## Search-engine images

Portraits and online-result thumbnails use DuckDuckGo Images only. This is a public search endpoint rather than an official image API, so automated lookup can occasionally be rate-limited or require a browser check. VideoShelf does not attempt to bypass those checks; cached images remain available and unresolved items keep their placeholder.

## Source layout

- `VideoShelf.cs` — main WinForms UI and local media browser
- `PortraitLookup.cs` — portrait search/cache
- `OnlineSearch.cs` — Torznab search/settings
- `OnlineThumbnailLookup.cs` — search-engine result thumbnails
- `AppDataPaths.cs` — VideoShelf data paths and one-time PeopleShelf migration
- `Start.cmd` — minimal Windows compiler/launcher
- `VideoShelf.csproj` — Visual Studio/MSBuild project

## Build validation

The repository includes a Windows GitHub Actions workflow. Pushes and pull requests to `main` compile the same sources and upload `VideoShelf.exe` as a workflow artifact.
