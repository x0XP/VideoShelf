VideoShelf 1.7.11 — 14 September 2026
--------------------------------------
- Added selectable subtitle tracks to the built-in streaming player, including Auto, Off and manual track selection.
- Auto subtitle selection prefers the Windows UI language while deprioritising signs/songs, forced and commentary tracks when a normal dialogue track is available.
- Added support for matching external subtitle files inside torrents (`.srt`, `.ass`, `.ssa`, `.vtt`, `.sub`, `.sup`) and loading them into LibVLC without delaying video playback.
- External subtitle matching uses cleaned filenames and episode identifiers so multi-episode torrents do not indiscriminately attach unrelated subtitle files.
- Added video volume controls with a percentage slider and Mute/Unmute behaviour that restores the previous audible level.
- Reworked the player transport area into two rows so subtitle and volume controls do not compress the seek timeline.

VideoShelf 1.7.10 — 14 September 2026
--------------------------------------
- Improved online thumbnail matching by carrying the original VideoShelf search into image lookup.
- Improved episode-specific artwork lookup and refreshed thumbnail caching so older mismatched previews are not reused.
- Strengthened DuckDuckGo Safe Search-off requests and prefers original images before lower-quality thumbnails.
- Slightly reduced image request frequency to avoid unnecessary search-engine bursts.

VideoShelf repository baseline — 10 September 2026
------------------------------------------------
- Project renamed from PeopleShelf to VideoShelf.
- Added automatic migration of existing PeopleShelf local settings and image caches.
- Added an MSBuild project and Windows GitHub Actions build validation.

VideoShelf 1.7 — 10 September 2026
--------------------------------------
- Rebuilt the application shell around the approved Xdolf-inspired VideoShelf mock-up rather than the earlier stock WinForms layout.
- Added the permanent left navigation rail for Home, Search, Collections, Downloads, Streaming and Settings, plus the custom VideoShelf title/status bars.
- Removed the slogans/quotes from the title and lower-left areas.
- Reworked Home into collection artwork cards with the Xdolf-style blue/red accent treatment and a matching dark sort control.
- Reworked online discovery into stacked torrent result cards with artwork, tags, resolution, size/date, seeders, leechers and source information.
- Added a persistent selected-result inspector with wrapped title, aligned metadata, artwork, tags and the large Download locally / Stream locally actions from the mock-up.
- Replaced visible native white filter controls with custom dark VideoShelf filters and matching popup menus.
- Dark-themed the search-results scrollbar and local-video ListView scrollbar so Windows-native white strips no longer break the interface.
- Owner-drew the complete local-video header and dynamically fits the final SUBFOLDER column to consume otherwise-unused header space without using the resize-feedback approach that previously caused instability.
- Added a mock-up-style framed search input with search glyph.
- Added Download/Streaming sidebar activity badges and preserved the existing transfer activity views.
- Added **View files** for seeded torrent results. It resolves torrent metadata only and displays the torrent manifest without intentionally starting media payload transfer.
- Added a runtime self-test that verifies the bundled MonoTorrent version exposes metadata-only startup support required by View files.
- Preserved the hard zero-seeder invariant: results reporting zero seeders are rejected by parsing and filtered again before rendering.
- Expanded GitHub Actions visual validation to render the actual compiled WinForms controls for Home, collection and search states.

VideoShelf 1.6 — 10 September 2026
--------------------------------------
- Added a distinct **Download locally** action to seeded online results.
- Download locally asks for a destination before any media transfer starts, then uses VideoShelf's bundled MonoTorrent runtime for a persistent torrent download.
- Added a dedicated download window with progress, torrent state, transfer rate, received-data figures, cancellation and Open folder.
- Replaced the external-handler streaming handoff with VideoShelf's own torrent streaming runtime.
- Added an embedded LibVLCSharp WinForms player for **Stream locally**.
- Streaming uses MonoTorrent's streaming provider and a localhost HTTP stream consumed by the embedded player.
- Multi-file torrents expose supported video files in the player and prioritise the selected file while marking unrelated files DoNotDownload.
- Added Play/Pause, Stop and seek controls to the streaming window.
- Streaming data is stored in a temporary `%LOCALAPPDATA%\VideoShelf\StreamingCache` directory and removed on player close on a best-effort basis.
- Online browsing/searching remains metadata-only; neither Download nor Stream begins until the user explicitly selects that action.
- The zero-seeder rule remains enforced in both parsing and rendering.
- Added `TransferBridge.cs` to keep the lightweight .NET Framework shell separate from the transfer engine.
- Added a self-contained .NET 8 `TransferHost` using MonoTorrent 3.0.2, LibVLCSharp.WinForms 3.10.1 and the bundled Windows LibVLC runtime.

VideoShelf 1.5 — 10 September 2026
--------------------------------------
- Reworked the VideoShelf visual language to match the rebuilt Xdolf click-GUI.
- Added a shared Xdolf-inspired palette based on near-black `#0A0C10`, outline `#3B414B`, blue `#329CFF`, red `#FF2020`, compact light text and muted secondary text.
- Replaced stock Windows ToolTip rendering with an owner-drawn information card inspired directly by Xdolf's `ClickGuiTooltip`.
- Tooltips now use a dark body, subtle shadow, hard outline, red left accent, blue top accent, compact heading, blue INFO marker, divider and muted body copy.
- Routed online-result hover details through the custom tooltip renderer instead of the native ListView tooltip.
- Restyled collection cards with Xdolf-like hard outlines, blue top edges, red left edges and darker hover states.
- Restyled buttons, search fields, list surfaces and the Add folder dialog to use the same visual system.

VideoShelf 1.4 — 10 September 2026
--------------------------------------
- Added an in-app **Add folder** control to the main library view.
- Added Ctrl+N as a shortcut for creating a new collection.
- New folders are created directly inside the currently selected VideoShelf library.
- Display names can contain characters that Windows does not allow in folder names.
- VideoShelf stores the exact display name in `.videoshelf-name` and uses a safe physical directory name underneath.
- Added automated coverage for safe physical folder names while preserving exact display names.

VideoShelf 1.3 — 10 September 2026
--------------------------------------
- Added a 96x54 thumbnail beside each online torrent result.
- Thumbnails are found using DuckDuckGo Images only; no metadata API is used.
- Torrent titles are cleaned before image lookup to remove common resolution, codec, source, audio and release noise.
- Different quality releases of the same cleaned title share a cached thumbnail.
- Progressive background loading keeps the results list usable while images load.
- Search-engine thumbnails are cached under Local AppData for later sessions.
- Placeholder artwork is retained when no usable image is available.
- Hover tooltips show the cleaned image query and image source when known.

VideoShelf 1.2 — 10 September 2026
--------------------------------------
- Added optional Torznab-compatible online video search.
- Added automatic background search when a collection is opened.
- Added a dedicated online results view with title, resolution, size, seeders, leechers/peers, source and publication date.
- Added resolution detection/filtering for 2160p/4K/UHD, 1080p/FHD, 720p and Other.
- Added highest-seeder-first online sorting.
- Added Open selected result and Copy link actions.
- Added online source configuration and Windows DPAPI protection for saved API keys.
- Results reporting zero seeders are rejected by the parser and filtered again before rendering.
- Local media is never uploaded by VideoShelf.
