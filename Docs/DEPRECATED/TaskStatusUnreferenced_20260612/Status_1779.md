# Status_1779

Agent ID: 1779
Domain: Applied Lore Static Reader / Protosite
Evidence class: STATIC_SOURCE unless stated otherwise.

## Tasks

- [x] 01 Create/update Status_1779.md with all 20 tasks.
- [x] 02 Create/update Rationale_1779.md with reader/protosite decisions.
- [x] 03 Inspect reader.html and document current capabilities/gaps.
- [x] 04 Inspect surface index, cluster index, localization status index, and packet JSON shape.
- [x] 05 Checkpoint: implementation plan chosen.
- [x] 06 Add explicit 15-locale selector and RTL handling for ar_SA/he_IL.
- [x] 07 Add/improve filters for external_site, in_game_wiki, packet title, scanner, field_note, terminal, audio, codex/wiki labels.
- [x] 08 Add/improve search across title, packet ID, article ID, body text, cluster, release set, and locale where available.
- [x] 09 Add/improve cluster/navigation view using Publication_Cluster_Index.csv with sane malformed/missing fallbacks.
- [x] 10 Checkpoint: reopen/read changed reader.html and verify no CDN/internet dependency.
- [x] 11 Add localization status visibility without injecting draft labels into article/player bodies.
- [x] 12 Add spoiler/status display if indexed; show not indexed where absent.
- [x] 13 Add packet detail rendering from AppliedContent packet JSON with graceful local-fetch failure.
- [x] 14 Add editor/controller-only surface brightness canon warning for suspect dark-surface keywords.
- [x] 15 Checkpoint: run local static smoke test if possible.
- [x] 16 Validate reader.html syntax with available tools or documented static checks.
- [x] 17 Test representative locales en_US, ru_RU, ja_JP, ar_SA, he_IL, pt_BR or document limits.
- [x] 18 Update AppliedContent README only if reader usage instructions need precise change.
- [x] 19 Write HANDOFF_1779.md with future index/browser QA gaps.
- [x] 20 Final verification, update status, append LOG_1779.md.

## Checkpoints

### Task 05

Plan:
- Keep `reader.html` as dependency-free static HTML/JS/CSS.
- Use `Publication_Surface_Index.csv` for public-site/wiki page rows.
- Use `Publication_Cluster_Index.csv` for cluster navigation and spoiler tier.
- Use `Localization_Status_Index.md` only as controller-facing summary text.
- Load `packets/*.json` and `packets/*.packets.json` lazily for selected packet detail surfaces.
- Do not rewrite Markdown or packet text. Status/draft/spoiler warnings stay in controller UI only.
- Treat browser/local fetch limits as expected static-protosite behavior.

Rejected complexity:
- No build step.
- No generated bundle.
- No CDN/library dependency.
- No runtime markdown or JSON interpretation claim for Unity gameplay.

First-20-minutes route effect: removes a lore inspection blocker for start-here, public/wiki, scanner, terminal, field note, and audio packet review.

### Task 10

Reopened `reader.html`.
Static dependency check:
- No external script or link import.
- No `https://` dependency.
- Only HTTP URL string is local `http://127.0.0.1:8788/reader.html` instruction text.

### Task 15

`python -m http.server 8788 --bind 127.0.0.1 --directory Docs/Lore/AppliedContent` could not be started because port 8788 was already occupied and unresponsive during smoke attempts.
Temporary smoke server used port 8790 and was stopped after test.
HTTP HEAD checks returned 200 for:
- `reader.html`
- `Publication_Surface_Index.csv`
- `Publication_Cluster_Index.csv`
- `Localization_Status_Index.md`
- `external_site/ru_RU/P416_SITE_WIKI_START_HERE_CLUSTER.md`
- `packets/RS084_SITE_WIKI_NAVIGATION_CLUSTERS.packets.json`

### Task 20

Final verification:
- `reader.html` JavaScript parsed with Node `new Function`.
- Required DOM IDs present.
- Packet JSON parsed: 100 JSON files, 460 packets, zero parse failures.
- Representative locale static checks passed for `en_US`, `ru_RU`, `ja_JP`, `ar_SA`, `he_IL`, `pt_BR`.
- `diff --check` reported no whitespace errors; Git warned CRLF may replace LF later.
- Playwright not installed; no browser-render automation performed.
- No task-launched Python HTTP server remains running.

Polish verification:
- Added status bucket display while preserving raw status values.
- Added packet warm-up accounting for release bundles, direct packet fallback, and unresolved packet loads.
- Added controller-facing localization length/overflow risk warnings and focus/live-region accessibility hooks.
- Packet warm-up simulation against disk: 13,800 surface rows, 92 release sets, 460 packets, 91 bundle files loaded, one bundle fallback, nine direct fallback packets, zero unresolved packets.
- Temporary HTTP smoke on port 8791 returned 200 for reader, CSV indexes, localization status, sample Markdown, direct packet JSON, and release-set packet JSON. Server stopped.
- `dotnet build` was not run. Existing Unity `dotnet` processes were observed, so build throttling was preserved.

### C# Apex Follow-Up

Completed source patch:
- `Assets/_Project/Scripts/UI/ReadableMainMenuOverlay1428.cs`: removed the cold `Camera.main` fallback from overlay camera resolution and cached the controller-provided menu camera instead.

Static verification:
- `ReadableMainMenuOverlay1428.cs` now has no `Camera.main`, `GlobalRegistry.Get`, LINQ, `string.Format`, `.ToString()`, or `GetComponent<...>` hot-token match in the targeted scan.
- `MainMenuController.Tick` only consumes menu input and stores unscaled delta; presentation remains in `MainMenuController.LateFrameTick`.
- `MainMenuController.TryGetReadableOverlayCamera` returns the already-authored `mainMenuCamera`, so overlay camera identity is cold/cached.
- DataVault samples inspected in UI/atmosphere routes use single acquired write views with `finally` release; gas solver state ownership uses one mutation guard mask with release bookkeeping.
- `Assets/_Project/Scripts/UI` orphan `.meta` scan passed.
- `Assets/_Project/Scripts/UI/MainMenuAudioIntegration.cs` and `.cs.meta` are both absent, so that deleted pair is not orphaned.
- `dotnet build` was not run. Unity `dotnet.exe` was already active, so validation stayed static.

### C# Ecosystem Guard Follow-Up

Completed source patch:
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs`: guarded read-side Vault snapshots and tuning/counter reads with the existing `TryAcquireSymbiosisMutationGuard` / `ReleaseSymbiosisMutationGuard` helpers.

Static verification:
- Snapshot copies now hold one buffer guard at a time and release it in `finally`.
- `TryReadSymbiosisTuning` and `TryReadSymbiosisCounter` now release their guards in `finally`.
- No new DTOs, arrays, lists, dictionaries, or helper classes were introduced.
- Targeted hot-token scan of the patched solver found no `Camera.main`, `GlobalRegistry.Get`, `GetComponent`, LINQ, `string.Format`, `.ToString()`, scene search, or `GameObject.Find` hits.
- Combined patched-file scan still reports `FindObjectsByType` only in the existing menu overlay `ResolveFontCold` path.
- `Assets/_Project/Scripts/AI/Ecosystem` orphan `.meta` scan passed.
- `dotnet build` was not run because Unity `dotnet.exe` remained active.

### C# Audio/Persistence Apex Follow-Up

Completed source patches:
- `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs`: moved vocal-warning evaluation from synchronous hot-path `.Run()` shape to dispatcher-owned scheduled jobs in `Simulation`; presentation finalization now occurs only in `VisualSync`/late presentation and force-completes only during teardown/rebind.
- `Assets/_Project/Scripts/SaveManager.cs`: flattened WFC outpost storm dirty-sector processing from duplicate O(n^2) scans into one bounded stackalloc unique-sector pass, preserved bounded per-sector writes, and flagged overflow through existing black-box telemetry.

Static verification:
- Unity `validate_script` returned 0 diagnostics for `VocalWarningSystem.cs`, `SaveManager.cs`, `ShinobuFloraFaunaSymbiosisSolver.cs`, `ReadableMainMenuOverlay1428.cs`, and `SaveSlotManagerWindow.cs`.
- Unity console error/warning read returned 0 entries after validation.
- `SaveSlotManagerWindow.cs` call to `SaveManager.CollectAllKnownArtifactPaths` validates; earlier console error was stale after source reload.
- Touched C# files have balanced braces and preprocessor blocks.
- Scoped orphan `.meta` scan across touched script/editor folders returned 0.
- Targeted hot lookup scan reports only cold/editor/lifecycle paths: `FindObjectsByType` in cold menu/font/lifecycle code, async thumbnail wait, and editor-only mock threat `.Run()`.
- `dotnet build` was not run because `dotnet.exe` was already active.
