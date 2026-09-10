VideoShelf repository baseline — 10 September 2026
------------------------------------------------
- Project renamed from PeopleShelf to VideoShelf.
- Added automatic migration of existing PeopleShelf local settings and image caches.
- Added an MSBuild project and Windows GitHub Actions build validation.

VideoShelf 1.4 — 10 September 2026
--------------------------------------
- Added an in-app **Add folder** control to the main library view.
- Added Ctrl+N as a shortcut for creating a new collection.
- New folders are created directly inside the currently selected VideoShelf library.
- Display names can contain characters that Windows does not allow in folder names.
- VideoShelf stores the exact display name in `.videoshelf-name` and uses a safe physical directory name underneath.
- Added an automated Windows self-test using the requested `re:zero` example. The test verifies that the physical directory contains no colon while the VideoShelf display name remains exactly `re:zero`.

VideoShelf 1.3 — 10 September 2026
--------------------------------------
- Added a 96x54 thumbnail beside each online torrent result.
- Thumbnails are found using DuckDuckGo Images only; no metadata API is used.
- Torrent titles are cleaned before image lookup to remove common resolution,
  codec, source, audio and release noise.
- Different quality releases of the same cleaned title share a cached thumbnail.
- Progressive background loading keeps the results list usable while images load.
- Search-engine thumbnails are cached under Local AppData for later sessions.
- Placeholder artwork is retained when no usable image is available.
- Hover tooltips show the cleaned image query and image source when known.

VideoShelf 1.2 — 10 September 2026

ADDED
- Optional Torznab-compatible online video search.
- Automatic background search when a person is opened (configurable).
- Dedicated online results view with title, resolution, size, seeders,
  leechers/peers, source and publication date.
- Resolution detection/filtering for 2160p/4K/UHD, 1080p/FHD, 720p and Other.
- Highest-seeder-first online sorting.
- Open selected result and Copy link actions.
- Online source configuration dialog.
- Windows DPAPI protection for a saved Torznab API key.

RULES / SAFETY
- Results reporting zero seeders are rejected by the parser and filtered again
  before rendering, so they never appear in the online list.
- VideoShelf remains provider-agnostic. Tracker/indexer configuration stays in
  the user's Torznab-compatible service rather than being hard-coded into the app.
- Local media is never uploaded by VideoShelf.

UNCHANGED
- Folder-per-person library model.
- Recursive local video discovery and default-player launch.
- DuckDuckGo portrait lookup/cache and manual image override.
- Search, sorting, keyboard shortcuts and people-to-videos navigation.
